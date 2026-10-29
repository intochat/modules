using ClickHouse.Driver;
using IntoChat.OS.Modules.ClickHouse.Query;
using IntoChat.OS.Connections.Platform.Integrations;

namespace IntoChat.OS.Modules.ClickHouse;

internal sealed class IntegrationClickHouseProvider(IModuleIntegrations<ClickHouseModule> integrations, IHttpClientFactory clients) : IClickHouseProvider, IIntegrationStartup
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ClickHouseDriverProvider? _current;
    private long _revision = -1;

    public async Task InitializeAsync(CancellationToken cancellationToken) => _ = await Materialize(cancellationToken);
    public string ProviderName => ClickHouseModule.DriverProviderName;

    internal async Task<ClickHouseDriverProvider> Materialize(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var handle = integrations.Get("clickhouse");
            var snapshot = await handle.ReadAsync(ct);
            if (snapshot.Status != IntegrationStatus.Ready) { throw new InvalidOperationException("The ClickHouse integration is unavailable."); }
            if (_current is not null && _revision == snapshot.Revision) { return _current; }
            var released = await handle.ReleaseAsync(ct);
            if (released.Snapshot.Status != IntegrationStatus.Ready) { throw new InvalidOperationException("The ClickHouse integration is unavailable."); }
            var connection = released.Values["ConnectionString"];
            _current = new(new ClickHouseClient(connection, clients, ClickHouseRegistration.HttpClientName), ClickHouseDriverProvider.DatabaseOf(connection));
            _revision = released.Snapshot.Revision;
            return _current;
        }
        finally { _gate.Release(); }
    }

    public async Task<ClickHouseQueryResult> QueryAsync(string sql, int maxRows, CancellationToken cancellationToken)
        => await (await Materialize(cancellationToken)).QueryAsync(sql, maxRows, cancellationToken);
    public async Task<IReadOnlyList<ClickHouseColumn>> DescribeAsync(string sql, CancellationToken cancellationToken)
        => await (await Materialize(cancellationToken)).DescribeAsync(sql, cancellationToken);
    public async Task<ClickHouseSchema> ReadSchemaAsync(string? table, CancellationToken cancellationToken)
        => await (await Materialize(cancellationToken)).ReadSchemaAsync(table, cancellationToken);
    public async Task<ClickHouseConnection> PingAsync(CancellationToken cancellationToken)
        => await (await Materialize(cancellationToken)).PingAsync(cancellationToken);
}
