using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Connections;

namespace IntoChat.OS.Modules.ClickHouse.Query;

[GenerateSerializer, Alias("clickhouse.connection-state")]
internal sealed class ClickHouseConnectionState : ConnectionState { }

// Read-only door to a database. The integration key ("system/default") serves the deployment's database
// and holds no state; a connection key serves the user's database from the connection string
// sealed in this neuron's own state. Every query, schema read and ping is served live.
[DigitalBrain.Neuron(ClickHouseNames.NeuronType, State = "clickhouse")]
internal sealed class ClickHouseNeuron(
    IClickHouseProvider provider, IClickHouseSourceFactory sources)
    : ConnectionNeuron<ClickHouseConnectionState>, IClickHouse
{
    private const int MaxTableNameLength = 255;
    public async Task<ClickHouseQueryResult> Query(ClickHouseQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Sql))
        {
            throw new ClickHouseQueryException("sql must not be blank. " + ClickHouseQueryGuard.Reason);
        }

        if (query.MaxRows is < 1 or > ClickHouseQuery.MaxRowsLimit)
        {
            throw new ClickHouseQueryException($"maxRows must be between 1 and {ClickHouseQuery.MaxRowsLimit}.");
        }

        try
        {
            ClickHouseQueryGuard.Validate(query.Sql);
        }
        catch (ArgumentException error)
        {
            throw new ClickHouseQueryException(error.Message);
        }

        return await WithProvider(source => source.QueryAsync(query.Sql, query.MaxRows, cancellationToken), cancellationToken).ConfigureAwait(true);
    }
    public async Task<ClickHouseSchema> ReadSchema(ReadClickHouseSchema query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var table = string.IsNullOrWhiteSpace(query.Table) ? null : query.Table.Trim();
        if (table is not null && (table.Length > MaxTableNameLength || !table.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')))
        {
            throw new ClickHouseQueryException("table must be a plain table name: letters, digits and underscores.");
        }

        return await WithProvider(source => source.ReadSchemaAsync(table, cancellationToken), cancellationToken).ConfigureAwait(true);
    }
    public Task<ClickHouseConnection> ReadConnection(CancellationToken cancellationToken = default)
        => WithProvider(source => source.PingAsync(cancellationToken), cancellationToken);

    protected override async Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct)
    {
        var ping = await sources.ForConnectionString(credential.Value).PingAsync(ct).ConfigureAwait(true);
        return ping.Connected ? ConnectionProbeResult.Connected() : ConnectionProbeResult.Failing("The database did not answer.");
    }

    private Task<T> WithProvider<T>(Func<IClickHouseProvider, Task<T>> operation, CancellationToken cancellationToken)
    {
        if (!ConnectionKeys.Is(FullKey)) { return operation(provider); }
        (string Brain, string Connector, string Connection) key;
        try { key = Key; }
        catch (ArgumentException) { throw new UnauthorizedAccessException("A canonical connection key is required."); }
        if (key.Connector != "clickhouse" || key.Brain != BrainScope.CurrentId())
        { throw new UnauthorizedAccessException("The connection belongs to another brain."); }
        return UseCredentialAsync((credential, _) => operation(sources.ForConnectionString(credential.Value)), cancellationToken);
    }
}
