namespace IntoChat.OS.Modules.Postgres;

public sealed record PostgresTableInfo(
    string Name,
    string Kind,
    long? TotalRows,
    IReadOnlyList<PostgresColumn> Columns);

public sealed record PostgresSchema(
    string Database,
    IReadOnlyList<PostgresTableInfo> Tables);
