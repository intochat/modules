using IntoChat.OS.Modules.Postgres;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Modules.Postgres.Tables;

namespace IntoChat.OS.Modules.Postgres;

[GenerateSerializer, Alias("postgres.connection-state")]
internal sealed class PostgresConnectionState : ConnectionState { }

// Integration keys ("system/default") serve the deployment's database and hold no state; connection keys
// serve the user's database from the connection string sealed in this neuron's own state.
[DigitalBrain.Neuron(GrainType, State = "postgres")]
internal sealed class PostgresServerNeuron(
    IPostgresProvider platform, IConnectionPostgresProviderFactory connections, IConnectionTableStores tables)
    : ConnectionNeuron<PostgresConnectionState>, IPostgresServer, IPostgresTableHost
{
    internal const string GrainType = "postgres";
    public Task<PostgresQueryResult> Query(PostgresQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.MaxRows is < 1 or > PostgresQuery.MaxRowsLimit)
        {
            throw new PostgresQueryException($"maxRows must be between 1 and {PostgresQuery.MaxRowsLimit}.");
        }
        var parameters = query.Parameters ?? [];
        if (parameters.Length > PostgresQuery.MaxParameters)
        {
            throw new PostgresQueryException($"A query takes at most {PostgresQuery.MaxParameters} parameters.");
        }
        string sql;
        try { sql = PostgresQueryGuard.Normalize(query.Sql); }
        catch (ArgumentException error) { throw new PostgresQueryException(error.Message); }
        return WithProvider(provider => provider.QueryAsync(sql, query.MaxRows, parameters, CancellationToken.None));
    }
    public Task<PostgresSchema> ReadSchema(ReadPostgresSchema query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var table = string.IsNullOrWhiteSpace(query.Table) ? null : query.Table.Trim();
        if (table is not null && (table.Length > 255 || !table.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.')))
        {
            throw new PostgresQueryException("table must be a plain table name, optionally prefixed with its schema.");
        }
        return WithProvider(provider => provider.ReadSchemaAsync(table, CancellationToken.None));
    }
    public Task<PostgresConnection> ReadConnection() => WithProvider(provider => provider.PingAsync(CancellationToken.None));

    protected override async Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct)
    {
        var ping = await connections.ForConnectionString(credential.Value).PingAsync(ct);
        return ping.Connected ? ConnectionProbeResult.Connected() : ConnectionProbeResult.Failing("The database did not answer.");
    }

    Task IPostgresTableHost.Define(string table, TableDefinition definition)
        => WithTables(store => Done(store.DefineAsync(TablePlacement.Shared, table, definition, CancellationToken.None)));
    Task IPostgresTableHost.Drop(string table)
        => WithTables(store => Done(store.DropAsync(TablePlacement.Shared, table, CancellationToken.None)));
    Task<bool> IPostgresTableHost.Upsert(string table, TableDefinition definition, TableValue[] key, TableValue[] values)
        => WithTables(store => store.UpsertAsync(TablePlacement.Shared, table, definition, key, values, CancellationToken.None));
    Task<bool> IPostgresTableHost.Delete(string table, TableDefinition definition, TableValue[] key)
        => WithTables(store => store.DeleteAsync(TablePlacement.Shared, table, definition, key, CancellationToken.None));
    Task<TableValue[]?> IPostgresTableHost.Read(string table, TableDefinition definition, TableValue[] key)
        => WithTables(store => store.ReadAsync(TablePlacement.Shared, table, definition, key, CancellationToken.None));
    Task<TableValue[][]> IPostgresTableHost.Page(string table, TableDefinition definition, int offset, int limit)
        => WithTables(store => store.PageAsync(TablePlacement.Shared, table, definition, offset, limit, CancellationToken.None));

    private static async Task<bool> Done(Task operation) { await operation; return true; }

    // Inside the user's database the deployment's isolation policy does not apply: tables keep their names.
    private async Task<T> WithTables<T>(Func<ITableStore, Task<T>> operation)
    {
        if (!ConnectionKeys.Is(FullKey)) { throw new UnauthorizedAccessException("Only a connection hosts tables."); }
        RequireOwnBrain();
        try { return await UseCredentialAsync((credential, _) => operation(tables.ForConnectionString(credential.Value)), CancellationToken.None); }
        catch (InvalidOperationException error) when (error is not PostgresQueryException)
        { throw new PostgresUnavailableException("This table's connection is not available."); }
    }

    private void RequireOwnBrain()
    {
        (string Brain, string Connector, string Connection) key;
        try { key = Key; }
        catch (ArgumentException) { throw new UnauthorizedAccessException("A canonical connection key is required."); }
        if (key.Connector != "postgres" || key.Brain != BrainScope.CurrentId())
        { throw new UnauthorizedAccessException("The connection belongs to another brain."); }
    }

    private Task<T> WithProvider<T>(Func<IPostgresProvider, Task<T>> operation)
    {
        if (!ConnectionKeys.Is(FullKey)) { return operation(platform); }
        RequireOwnBrain();
        return UseCredentialAsync((credential, _) => operation(connections.ForConnectionString(credential.Value)), CancellationToken.None);
    }
}
