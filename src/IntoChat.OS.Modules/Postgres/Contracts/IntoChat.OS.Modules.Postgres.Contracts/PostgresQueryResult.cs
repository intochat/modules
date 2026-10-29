namespace IntoChat.OS.Modules.Postgres;

// Each cell is the cell's own JSON text (for example 42, "a" or null); JSON travels as text so
// the cell type needs no foreign serializer at the grain boundary.
public sealed record PostgresQueryResult(
    IReadOnlyList<PostgresColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    long RowCount,
    bool Truncated,
    double ElapsedMs);
