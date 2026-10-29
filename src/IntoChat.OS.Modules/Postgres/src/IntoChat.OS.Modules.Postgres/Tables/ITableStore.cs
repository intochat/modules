namespace IntoChat.OS.Modules.Postgres.Tables;

public interface ITableStore
{
    Task DefineAsync(TablePlacement placement, string table, TableDefinition definition, CancellationToken ct);
    Task DropAsync(TablePlacement placement, string table, CancellationToken ct);
    Task<bool> UpsertAsync(TablePlacement placement, string table, TableDefinition definition, TableValue[] key, TableValue[] values, CancellationToken ct);
    Task<bool> DeleteAsync(TablePlacement placement, string table, TableDefinition definition, TableValue[] key, CancellationToken ct);
    Task<TableValue[]?> ReadAsync(TablePlacement placement, string table, TableDefinition definition, TableValue[] key, CancellationToken ct);
    Task<TableValue[][]> PageAsync(TablePlacement placement, string table, TableDefinition definition, int offset, int limit, CancellationToken ct);
}
