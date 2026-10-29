using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Postgres.Tables;

[DigitalBrain.Contracts.NeuronAlias("postgres.table-descriptor"), DigitalBrain.Contracts.DefaultNeuronType(TableNeuron.GrainType)]
public interface ITableDescriptor
{
    Task<TableDescription> Describe(string owner);
}

[GenerateSerializer, Alias("postgres.table-description")]
public sealed record TableDescription(
    [property: Id(0)] string AppId,
    [property: Id(1)] string TableId,
    [property: Id(2)] TablePlacement Placement,
    [property: Id(3)] AcceptedTable Table);

internal sealed partial class TableNeuron : ITableDescriptor
{
    public async Task<TableDescription> Describe(string owner)
    {
        var caller = CallerContextStamper.Require();
        if (!CallerContextStamper.IsTrusted(caller) || caller.Kind == CallerKind.App) { throw new UnauthorizedAccessException("This table belongs to another brain or app."); }
        var scope = TableOwner.Parse(owner);
        if (scope.BrainId != BrainScope.CurrentId() || State.Owner != owner)
        { throw new UnauthorizedAccessException("This table belongs to another brain or app."); }
        await Tables(owner).RequireOpen();
        var table = State.Accepted ?? throw new PostgresQueryException("This table has not finished being defined.");
        return new(scope.AppId, FullKey, State.Placement ?? TablePlacement.Shared, table);
    }
}
