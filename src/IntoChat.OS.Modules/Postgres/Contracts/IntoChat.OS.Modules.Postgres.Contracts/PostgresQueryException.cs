namespace IntoChat.OS.Modules.Postgres;

// Carries a safe error with SQLSTATE, excluding server details and connection credentials.
public sealed class PostgresQueryException(string message) : InvalidOperationException(message);

public sealed class PostgresUnavailableException(string message) : ServiceUnavailableException(message);
