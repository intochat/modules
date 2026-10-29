using ClickHouse.Driver;
using IntoChat.OS.Modules.ClickHouse.Query;

namespace IntoChat.OS.Modules.ClickHouse;

// Opens a user's database from the connection string a connection neuron unsealed for one call.
internal interface IClickHouseSourceFactory
{
    IClickHouseProvider ForConnectionString(string connectionString);
}

internal sealed class ClickHouseClientSourceFactory(IHttpClientFactory clients) : IClickHouseSourceFactory
{
    public IClickHouseProvider ForConnectionString(string connectionString) => new ConnectionStringClickHouseProvider(connectionString, clients);

    // One client per call, disposed with it: nothing outlives the grain turn that opened the value.
    private sealed class ConnectionStringClickHouseProvider(string connectionString, IHttpClientFactory clients) : IClickHouseProvider
    {
        public string ProviderName => ClickHouseModule.DriverProviderName;

        private async Task<T> Execute<T>(Func<ClickHouseDriverProvider, Task<T>> operation)
        {
            using var client = new ClickHouseClient(connectionString, clients, ClickHouseRegistration.HttpClientName);
            return await operation(new ClickHouseDriverProvider(client, ClickHouseDriverProvider.DatabaseOf(connectionString))).ConfigureAwait(false);
        }
        public Task<ClickHouseQueryResult> QueryAsync(string sql, int maxRows, CancellationToken cancellationToken)
            => Execute(provider => provider.QueryAsync(sql, maxRows, cancellationToken));
        public Task<IReadOnlyList<ClickHouseColumn>> DescribeAsync(string sql, CancellationToken cancellationToken)
            => Execute(provider => provider.DescribeAsync(sql, cancellationToken));
        public Task<ClickHouseSchema> ReadSchemaAsync(string? table, CancellationToken cancellationToken)
            => Execute(provider => provider.ReadSchemaAsync(table, cancellationToken));
        public Task<ClickHouseConnection> PingAsync(CancellationToken cancellationToken)
            => Execute(provider => provider.PingAsync(cancellationToken));
    }
}
