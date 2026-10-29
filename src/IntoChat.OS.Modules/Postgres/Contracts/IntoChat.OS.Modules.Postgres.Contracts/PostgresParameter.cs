namespace IntoChat.OS.Modules.Postgres;

// Positional: the n-th parameter binds $n. Value is text; PostgreSQL casts it where the SQL says so.
public sealed record PostgresParameter(string Name, string? Value);
