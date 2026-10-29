using IntoChat.OS;

namespace IntoChat.OS.Modules.ClickHouse.Query;

[DigitalBrain.Contracts.NeuronAlias("clickhouse"), DigitalBrain.Contracts.DefaultNeuronType(ClickHouseNames.NeuronType)]
public interface IClickHouse
{
    [DigitalBrain.ReadOnlyCall]
    Task<ClickHouseQueryResult> Query(ClickHouseQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<ClickHouseSchema> ReadSchema(ReadClickHouseSchema query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<ClickHouseConnection> ReadConnection(CancellationToken cancellationToken = default);
}
