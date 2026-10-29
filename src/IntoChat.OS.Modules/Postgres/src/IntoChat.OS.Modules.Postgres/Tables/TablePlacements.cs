using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Connections.Platform.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DigitalBrain.Runtime.Orleans.Hosting;

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
internal interface ITablesSlot
{
    Task<string?> BoundConnection(TableOwner owner);
}

// The grain factory resolves per call: silo services are validated before Orleans registers it.
internal sealed class AppTablesSlot(IServiceProvider services) : ITablesSlot
{
    public const string Name = "tables";

    public async Task<string?> BoundConnection(TableOwner owner)
    {
        if (!owner.AppId.StartsWith(owner.BrainId + "/apps/", StringComparison.Ordinal)) { return null; } // a bare behavior owns no slots
        var app = await SiloNeurons.Get<IApp>(services, owner.AppId).Read();
        return app.Manifest.Slots.Any(slot => slot.Name == Name && slot.Connector == "postgres")
            && app.Connections.TryGetValue(Name, out var id)
            ? ConnectionKeys.For(owner.BrainId, "postgres", id) : null;
    }
}
