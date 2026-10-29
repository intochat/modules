namespace IntoChat.OS.Modules.ClickHouse.Query;

// Carries the server's own error text so the agent can correct a wrong column or function name.
public sealed class ClickHouseQueryException(string message) : InvalidOperationException(message);

public sealed class ClickHouseUnavailableException(string message) : InvalidOperationException(message);
