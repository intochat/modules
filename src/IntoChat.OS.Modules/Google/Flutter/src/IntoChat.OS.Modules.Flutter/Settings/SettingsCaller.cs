using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Flutter.Settings;

// The Settings app the OS ships, running in this brain: the only caller that may configure the controls its
// view shows, so no other app (or an edited Settings) can put a misleading label on a person's preference or entry.
internal static class SettingsCaller
{
    public const string App = "settings";

    public static Task<bool> IsShipped(INeuronProxies brain, string? scope) => IsShipped(brain, scope, App);

    // The named app the OS ships, unedited, calling through the app proxy from its install in this brain.
    public static async Task<bool> IsShipped(INeuronProxies brain, string? scope, string app)
    {
        if (scope is null || !CallerContextStamper.TryGet(out var caller) || caller.Kind != CallerKind.App
            || caller.StampedBy != TrustedEdge.AppProxy || caller.AppId != IApp.Key(scope, app)) { return false; }
        // Only ShippedApps.Publisher ("os"), as WorkspaceNeuron and AppStoreNeuron check: OsAppsOptions.TrustedPublishers
        // lives in the Kernel, which this module cannot reference; a host-trusted extra publisher does not get this authority.
        return ShippedApps.Is(await brain.Get<IApp>(caller.AppId).Read(), app);
    }
}
