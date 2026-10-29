namespace IntoChat.OS.Modules.Postgres;

internal sealed class IntegrationPostgresProvider(IPostgresSourceRegistry sources) : IPostgresProvider
{
    private async Task<PostgresProvider> Create(CancellationToken ct)
        => new(await sources.Get(Tables.TablePlacement.Shared, ct));
    public async Task<PostgresQueryResult> QueryAsync(string sql, int maxRows, IReadOnlyList<PostgresParameter> parameters, CancellationToken cancellationToken)
        => await (await Create(cancellationToken)).QueryAsync(sql, maxRows, parameters, cancellationToken);
    public async Task<PostgresSchema> ReadSchemaAsync(string? table, CancellationToken cancellationToken)
        => await (await Create(cancellationToken)).ReadSchemaAsync(table, cancellationToken);
    public async Task<PostgresConnection> PingAsync(CancellationToken cancellationToken)
        => await (await Create(cancellationToken)).PingAsync(cancellationToken);
}
