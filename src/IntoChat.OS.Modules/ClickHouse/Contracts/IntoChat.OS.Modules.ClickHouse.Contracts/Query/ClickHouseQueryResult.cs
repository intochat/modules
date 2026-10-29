namespace IntoChat.OS.Modules.ClickHouse.Query;

// Each cell is the cell's own JSON text (for example 42, "a" or null); JSON travels as text so
// the cell type needs no foreign serializer at the grain boundary.
public sealed record ClickHouseQueryResult(
    IReadOnlyList<ClickHouseColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    long RowCount,
    bool Truncated,
    double ElapsedMs);
