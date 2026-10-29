namespace IntoChat.OS.Modules.Postgres.Tables;

public sealed record TableColumn(string Name, string Type);

public sealed record TableDefinition(TableColumn[] Columns, string[] PrimaryKey);

// Json is a single JSON value, including quotes for text. Null is allowed outside the key.
public sealed record TableValue(string Column, string Json);

public sealed record AcceptedTable(string Table, TableDefinition Definition, long Revision);
