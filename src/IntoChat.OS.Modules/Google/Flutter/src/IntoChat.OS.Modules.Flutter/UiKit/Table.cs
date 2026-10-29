using IntoChat.OS.Modules.Flutter.Table;

namespace IntoChat.OS.Modules.Flutter;

internal sealed record TableReplace(string Title, IReadOnlyList<TableColumn> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
