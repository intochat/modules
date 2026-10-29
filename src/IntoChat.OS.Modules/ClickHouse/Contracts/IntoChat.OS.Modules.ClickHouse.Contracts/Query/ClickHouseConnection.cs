namespace IntoChat.OS.Modules.ClickHouse.Query;

public sealed record ClickHouseConnection(
    bool Connected,
    string Database,
    string? ServerVersion,
    string Provider);
