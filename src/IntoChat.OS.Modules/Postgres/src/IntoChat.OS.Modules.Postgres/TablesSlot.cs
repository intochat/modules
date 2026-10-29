using IntoChat.OS.Modules.Postgres.Tables;

namespace IntoChat.OS.Modules.Postgres;

/// <summary>
/// An app's reserved <c>tables</c> slot: when the app declares it with the postgres connector and binds it, the app's
/// tables are placed in that connection's database instead of the brain's own.
/// </summary>
public static class TablesSlot
{
    /// <summary>The reserved slot name.</summary>
    public const string Name = AppTablesSlot.Name;

    /// <summary>
    /// The connection key the owner's tables slot is bound to, or <see langword="null"/> when the owner is not an app
    /// of its brain, does not declare a postgres tables slot or has not bound it.
    /// </summary>
    public static Task<string?> BoundConnection(IServiceProvider services, TableOwner owner)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new AppTablesSlot(services).BoundConnection(owner);
    }
}
