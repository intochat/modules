using System.Diagnostics;
using System.Globalization;
using Npgsql;

namespace IntoChat.OS.Modules.Postgres;

public interface IPostgresProvider
{
    Task<PostgresQueryResult> QueryAsync(string sql, int maxRows, IReadOnlyList<PostgresParameter> parameters, CancellationToken cancellationToken);
    Task<PostgresSchema> ReadSchemaAsync(string? table, CancellationToken cancellationToken);
    Task<PostgresConnection> PingAsync(CancellationToken cancellationToken);
}
