namespace IntoChat.OS.Modules.ClickHouse.Query;

public sealed record ClickHouseQuery(
    string Sql,
    int MaxRows = ClickHouseQuery.DefaultMaxRows)
{
    public const int DefaultMaxRows = 200;
    public const int MaxRowsLimit = 1000;
}
