
namespace IntoChat.OS.Modules.Postgres;

[DigitalBrain.Contracts.NeuronAlias("postgres"), DigitalBrain.Contracts.DefaultNeuronType("postgres")]
public interface IPostgresServer
{
    // Server-side caps.
    [DigitalBrain.ReadOnlyCall]
    Task<PostgresQueryResult> Query(PostgresQuery query);

    // Reads tables and columns of the configured database; omit Table for the index.
    [DigitalBrain.ReadOnlyCall]
    Task<PostgresSchema> ReadSchema(ReadPostgresSchema query);

    [DigitalBrain.ReadOnlyCall]
    Task<PostgresConnection> ReadConnection();
}
