namespace IntoChat.OS.Modules.ClickHouse.Query;

public sealed record ClickHouseTableInfo(
    string Name,
    string Engine,
    long? TotalRows,
    IReadOnlyList<ClickHouseColumn> Columns);

public sealed record ClickHouseSchema(
    string Database,
    IReadOnlyList<ClickHouseTableInfo> Tables);
