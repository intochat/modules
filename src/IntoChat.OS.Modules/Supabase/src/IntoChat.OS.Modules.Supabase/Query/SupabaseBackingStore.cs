using System.Collections.Concurrent;
using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IntoChat.OS.Modules.Supabase;

internal sealed record SupabaseConnection(bool Connected, string Database, string? ServerVersion);

// The deployment's own Supabase database, reached through Integration "supabase". A person's
// Supabase database is a postgres connection, never this store.
internal interface ISupabaseBackingStore
{
    Task<SupabaseConnection> PingAsync(CancellationToken cancellationToken);
}

internal sealed class SupabaseBackingStore(IModuleIntegrations<SupabaseModule> integrations, IOptions<SupabaseModuleOptions> options)
    : ISupabaseBackingStore, IIntegrationStartup, IAsyncDisposable
{
    private readonly ConcurrentDictionary<long, NpgsqlDataSource> _sources = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NpgsqlDataSource? _current;
    private long _revision = -1;

    internal async Task<NpgsqlDataSource> Materialize(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var handle = integrations.Get("supabase");
            var snapshot = await handle.ReadAsync(ct);
            if (snapshot.Status != IntegrationStatus.Ready) { throw new InvalidOperationException("The Supabase backing integration is unavailable."); }
            if (_current is not null && _revision == snapshot.Revision) { return _current; }
            var released = await handle.ReleaseAsync(ct);
            if (released.Snapshot.Status != IntegrationStatus.Ready) { throw new InvalidOperationException("The Supabase backing integration is unavailable."); }
            var settings = SupabaseConnectionSettings.Parse(released.Values["ConnectionString"]);
            _current = _sources.GetOrAdd(released.Snapshot.Revision, _ => NpgsqlDataSource.Create(settings.ConnectionString));
            _revision = released.Snapshot.Revision;
            return _current;
        }
        finally { _gate.Release(); }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (options.Value.UseBackingStore) { _ = await Materialize(cancellationToken); }
    }

    public async Task<SupabaseConnection> PingAsync(CancellationToken cancellationToken)
    {
        var source = await Materialize(cancellationToken);
        var database = new NpgsqlConnectionStringBuilder(source.ConnectionString).Database ?? "postgres";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await using var command = source.CreateCommand("SELECT version()");
            return new(true, database, (string?)await command.ExecuteScalarAsync(deadline.Token));
        }
        catch (Exception error) when (error is NpgsqlException || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new(false, database, null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values) { await source.DisposeAsync(); }
    }
}
