namespace IntoChat.OS.Modules.Postgres;

public sealed record PostgresConnection(
    bool Connected,
    string Database,
    string? ServerVersion,
    string Provider);
