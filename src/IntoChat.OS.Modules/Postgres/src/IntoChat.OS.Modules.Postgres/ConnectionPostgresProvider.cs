using IntoChat.OS.Modules.Postgres;
using Npgsql;

namespace IntoChat.OS.Modules.Postgres;

internal sealed class ConnectionPostgresProviderFactory : IConnectionPostgresProviderFactory
{
    public IPostgresProvider ForConnectionString(string connectionString) => new ConnectionPostgresProvider(connectionString);
}

internal sealed class ConnectionPostgresProvider(string connectionString) : IPostgresProvider
{
    private async Task<T> Execute<T>(Func<PostgresProvider, Task<T>> operation)
    {
        await using var source = NpgsqlDataSource.Create(PostgresConnectionSettings.Parse(connectionString).ConnectionString);
        return await operation(new PostgresProvider(source));
    }
    public Task<PostgresQueryResult> QueryAsync(string sql, int maxRows, IReadOnlyList<PostgresParameter> parameters, CancellationToken cancellationToken)
        => Execute(provider => provider.QueryAsync(sql, maxRows, parameters, cancellationToken));
    public Task<PostgresSchema> ReadSchemaAsync(string? table, CancellationToken cancellationToken)
        => Execute(provider => provider.ReadSchemaAsync(table, cancellationToken));
    public Task<PostgresConnection> PingAsync(CancellationToken cancellationToken)
        => Execute(provider => provider.PingAsync(cancellationToken));
}
