namespace IntoChat.OS.Modules.Postgres.Tests.Integration;

internal sealed class PostgresTestControls : IPostgresProvider
{
    public Task<PostgresQueryResult> QueryAsync(string sql, int maxRows, IReadOnlyList<PostgresParameter> parameters, CancellationToken cancellationToken)
    {
        Assert.Equal(10, maxRows);
        if (parameters.Count == 1)
        {
            Assert.Equal("select id from people where country = $1", sql);
            Assert.Equal("u's; drop table people", parameters[0].Value);
        }
        else { Assert.Equal("select id from people", sql); }
        return Task.FromResult(new PostgresQueryResult([new("id", "int4", "number")], [["7"]], 1, false, 0));
    }

    public Task<PostgresSchema> ReadSchemaAsync(string? table, CancellationToken cancellationToken)
    {
        Assert.Equal("reporting.people", table);
        return Task.FromResult(new PostgresSchema("sample", [new("reporting.people", "BASE TABLE", null, [new("id", "int4", "number")])]));
    }

    public Task<PostgresConnection> PingAsync(CancellationToken cancellationToken)
        => Task.FromResult(new PostgresConnection(true, "sample", "PostgreSQL", "Npgsql"));
}
