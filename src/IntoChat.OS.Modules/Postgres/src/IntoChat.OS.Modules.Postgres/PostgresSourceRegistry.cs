using System.Collections.Concurrent;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Modules.Postgres.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IntoChat.OS.Modules.Postgres;

public interface IPostgresSourceRegistry
{
    Task<NpgsqlDataSource> Get(TablePlacement placement, CancellationToken ct = default);
}

internal sealed class PostgresSourceRegistry(IModuleIntegrations<PostgresModule> integrations, IOptions<PostgresModuleOptions> options) : IPostgresSourceRegistry, IAsyncDisposable, IIntegrationStartup
{
    private readonly ConcurrentDictionary<(long Revision, TablePlacement Placement), NpgsqlDataSource> _sources = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NpgsqlConnectionStringBuilder? _platform;
    private NpgsqlConnectionStringBuilder? _admin;
    private long _revision = -1;
    private bool _adminMaterialized;

    public async Task InitializeAsync(CancellationToken cancellationToken)
        => _ = await Get(TablePlacement.Shared, cancellationToken);

    // Serves the deployment's own placements; a Connection placement runs inside its connection neuron.
    public async Task<NpgsqlDataSource> Get(TablePlacement placement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.Kind == TablePlacementKind.Connection || placement.Kind == TablePlacementKind.BrainDatabase && string.IsNullOrEmpty(placement.Name))
        { throw new ArgumentException("The deployment's Postgres sources serve only Shared and BrainDatabase placements.", nameof(placement)); }
        await _gate.WaitAsync(ct);
        try
        {
            var handle = integrations.Get("postgres");
            var snapshot = await handle.ReadAsync(ct);
            if (snapshot.Status != IntegrationStatus.Ready) { throw new PostgresUnavailableException("The Postgres integration is unavailable."); }
            var perBrain = options.Value.Isolation == PostgresIsolation.DatabasePerBrain;
            var needAdmin = perBrain || placement.Kind == TablePlacementKind.BrainDatabase;
            if (_platform is null || _revision != snapshot.Revision || needAdmin && !_adminMaterialized)
            {
                var released = await handle.ReleaseAsync(ct);
                if (released.Snapshot.Status != IntegrationStatus.Ready) { throw new PostgresUnavailableException("The Postgres integration is unavailable."); }
                var platform = PostgresConnectionSettings.Parse(released.Values["ConnectionString"]);
                var admin = needAdmin && released.Values.TryGetValue("AdminConnection", out var value) ? PostgresConnectionSettings.Parse(value) : null;
                if (perBrain && admin is null) { throw new InvalidOperationException("AdminConnection is required when Postgres isolation is DatabasePerBrain."); }
                _platform = platform;
                _admin = admin;
                _adminMaterialized = needAdmin;
                _revision = released.Snapshot.Revision;
            }
            var settings = placement.Kind == TablePlacementKind.Shared ? _platform : _admin
                ?? throw new PostgresUnavailableException("This table's database is not reachable from this deployment.");
            var connection = new NpgsqlConnectionStringBuilder(settings.ConnectionString);
            if (placement.Kind == TablePlacementKind.BrainDatabase) { connection.Database = placement.Name; }
            return _sources.GetOrAdd((_revision, placement), _ => NpgsqlDataSource.Create(connection.ConnectionString));
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values) { await source.DisposeAsync(); }
    }
}
