namespace IntoChat.OS.Modules.Postgres.Tables;

// A Connection placement's table operations, run inside the connection's own neuron so the
// credential never leaves it. Internal: scripts reach tables only through ITable.
[DigitalBrain.Contracts.NeuronAlias("postgres.table-host"), DigitalBrain.Contracts.DefaultNeuronType(PostgresServerNeuron.GrainType)]
internal interface IPostgresTableHost
{
    Task Define(string table, TableDefinition definition);
    Task Drop(string table);
    Task<bool> Upsert(string table, TableDefinition definition, TableValue[] key, TableValue[] values);
    Task<bool> Delete(string table, TableDefinition definition, TableValue[] key);
    Task<TableValue[]?> Read(string table, TableDefinition definition, TableValue[] key);
    Task<TableValue[][]> Page(string table, TableDefinition definition, int offset, int limit);
}

internal interface IConnectionTableStores
{
    ITableStore ForConnectionString(string connectionString);
}

internal sealed class ConnectionTableStores : IConnectionTableStores
{
    public ITableStore ForConnectionString(string connectionString) => new TableStore(connectionString);
}
