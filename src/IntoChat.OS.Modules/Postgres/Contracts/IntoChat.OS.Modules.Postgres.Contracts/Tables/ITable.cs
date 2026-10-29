using DigitalBrain.Contracts;
using DigitalBrain.Access;

namespace IntoChat.OS.Modules.Postgres.Tables;

// Use a unique grain key per installed table. First Define binds it permanently to the
// stamped brain and app; every subsequent operation requires that same caller scope.
// Each caller acts as its own brain; the table itself checks the stamped owner on every operation.
[Shared]
[NeuronAlias("postgres.table"), DefaultNeuronType("postgres.table")]
public interface ITable
{
    Task<AcceptedTable> Define(TableDefinition definition);
    Task<bool> Upsert(TableValue[] key, TableValue[] values);
    Task<bool> Delete(TableValue[] key);
    Task<TableValue[]?> Read(TableValue[] key);
    Task<TableValue[][]> Page(int offset = 0, int limit = 200);
}
