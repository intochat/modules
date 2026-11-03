using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Postgres.Tables;

internal interface ITablePlacements
{
    Task<TablePlacement> Resolve(TableOwner owner, CancellationToken ct);
}

// An app's bound tables slot wins; otherwise the deployment's isolation policy decides.
internal sealed class TablePlacements(IOptions<PostgresModuleOptions> options, BrainDatabases databases, ITablesSlot slot) : ITablePlacements
{
    public async Task<TablePlacement> Resolve(TableOwner owner, CancellationToken ct)
    {
        if (await slot.BoundConnection(owner) is { } key) { return TablePlacement.Connection(key); }
        return options.Value.Isolation == PostgresIsolation.DatabasePerBrain
            ? TablePlacement.BrainDatabase(await databases.Ensure(owner.BrainId, ct))
            : TablePlacement.Shared;
    }
}

// The connection key an app's reserved tables slot is bound to, if it declares and binds one.
public interface ITablesSlot
{
    Task<string?> BoundConnection(TableOwner owner);
}

internal sealed class NullTablesSlot : ITablesSlot
{
    public Task<string?> BoundConnection(TableOwner owner) => Task.FromResult<string?>(null);
}
