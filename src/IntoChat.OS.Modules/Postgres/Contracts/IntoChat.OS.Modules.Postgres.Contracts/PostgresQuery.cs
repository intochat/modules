namespace IntoChat.OS.Modules.Postgres;

public sealed record PostgresQuery(
    string Sql,
    int MaxRows = PostgresQuery.DefaultMaxRows,
    PostgresParameter[]? Parameters = null)
{
    public const int DefaultMaxRows = 200;
    public const int MaxRowsLimit = 1000;
    public const int MaxParameters = 16;
}
