namespace IntoChat.OS.Modules.Postgres;

// TableType is the UI column domain; PostgreSqlType preserves the server's database type.
public sealed record PostgresColumn(
    string Name,
    string PostgreSqlType,
    string TableType);
