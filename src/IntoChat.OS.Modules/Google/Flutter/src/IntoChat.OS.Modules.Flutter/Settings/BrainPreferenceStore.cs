using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Spi;
using IntoChat.OS.Runtime;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Flutter.TextField;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Flutter.Settings;

// The member-writable preferences of a brain, as text fields under {brain}/settings. The HTTP preference endpoints and
// the preference neuron both read and write through here, so they share one store and one caller rule.
internal static class BrainPreferenceStore
{
    public const string NewTabDefault = "ino";

    public static readonly (string Key, bool Default)[] NotificationKeys =
    [
        ("agent-finished", true),
        ("needs-approval", true),
        ("connection-errors", true),
        ("product-updates", false),
    ];

    // The OS's own services (ShippedApps.Services, which Desktop.Hidden also is) never appear as apps a person can choose.
    public static readonly HashSet<string> Hidden = [.. ShippedApps.Services];

    public sealed record Choice(string Name, string Title);

    // Installed apps (App callers or calls through the app proxy) cannot change a person's preferences,
    // as brain entries refuse them.
    public static bool IsAppCaller()
        => CallerContextStamper.TryGet(out var caller) && (caller.Kind == CallerKind.App || caller.StampedBy == TrustedEdge.AppProxy);

    public static bool IsNotificationKey(string key) => NotificationKeys.Any(known => known.Key == key);

    private static string NotificationField(string scope, string key) => scope + "/settings/notifications/" + key;

    private static string NewTabField(string scope) => scope + "/settings/workspace/new-tab-app";

    public static async Task<bool> ReadNotification(INeuronProxies brain, string scope, string key, CancellationToken ct)
    {
        var fallback = NotificationKeys.First(known => known.Key == key).Default;
        var state = await brain.Get<ITextField>(NotificationField(scope, key)).Read().WaitAsync(ct);
        return state.Value switch { "on" => true, "off" => false, _ => fallback };
    }

    public static async Task<Dictionary<string, bool>> ReadNotifications(INeuronProxies brain, string scope, CancellationToken ct)
    {
        var result = new Dictionary<string, bool>();
        foreach (var (key, _) in NotificationKeys) { result[key] = await ReadNotification(brain, scope, key, ct); }
        return result;
    }

    public static Task WriteNotification(INeuronProxies brain, string scope, string key, bool on, CancellationToken ct)
    {
        if (!IsNotificationKey(key)) { throw new ArgumentException("Unknown notification preference.", nameof(key)); }
        return brain.Get<ITextField>(NotificationField(scope, key)).SetValue(on ? "on" : "off").WaitAsync(ct);
    }

    // The apps a new tab may open: every titled, live app of the caller's brain except the hidden ones, Ino first.
    public static async Task<Choice[]> NewTabChoices(INeuronProxies brain, IBrainObserver kernel, string scope, CancellationToken ct)
    {
        var found = new List<Choice>();
        await foreach (var instance in kernel.Instances<IApp>(ct))
        {
            if (IApp.NameOf(scope, instance.Key) is not { } name || Hidden.Contains(name)) { continue; }
            var app = await brain.Get<IApp>(instance.Key).Read().WaitAsync(ct);
            if (!string.IsNullOrWhiteSpace(app.Manifest.Title) && app.MigrationTarget is null) { found.Add(new(name, app.Manifest.Title)); }
        }
        return [.. found.OrderBy(choice => choice.Name == NewTabDefault ? 0 : 1).ThenBy(choice => choice.Title, StringComparer.OrdinalIgnoreCase)];
    }

    public static async Task<string> ReadNewTab(INeuronProxies brain, string scope, Choice[] choices, CancellationToken ct)
    {
        var stored = (await brain.Get<ITextField>(NewTabField(scope)).Read().WaitAsync(ct)).Value;
        return choices.Any(choice => choice.Name == stored) ? stored! : NewTabDefault;
    }

    // False when the name is not one of the choices; nothing is written then.
    public static async Task<bool> WriteNewTab(INeuronProxies brain, IBrainObserver kernel, string scope, string? name, CancellationToken ct)
    {
        var choices = await NewTabChoices(brain, kernel, scope, ct);
        if (name is null || !choices.Any(choice => choice.Name == name)) { return false; }
        await brain.Get<ITextField>(NewTabField(scope)).SetValue(name).WaitAsync(ct);
        return true;
    }
}
