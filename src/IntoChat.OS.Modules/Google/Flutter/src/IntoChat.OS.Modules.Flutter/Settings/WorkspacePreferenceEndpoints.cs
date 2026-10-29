using DigitalBrain.Runtime.Orleans;
using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IntoChat.OS.Modules.Flutter.Settings;

// Per-brain workspace preferences. Today that is the app a new tab opens, stored under {brain}/settings/workspace
// as a text field holding the app name. Any member may read and change it.
internal static class WorkspacePreferenceEndpoints
{
    public const string Default = BrainPreferenceStore.NewTabDefault;

    internal static HashSet<string> Hidden => BrainPreferenceStore.Hidden;

    internal sealed record Preferences(string NewTabApp, BrainPreferenceStore.Choice[] Choices);

    internal sealed record Input(string? NewTabApp);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = BrainRoutes.Group(endpoints, "/preferences/workspace");
        group.MapGet("", async (INeuronProxies brain, IBrainObserver kernel, CancellationToken ct) => Results.Ok(await Read(brain, kernel, BrainScope.CurrentId(), ct)));
        group.MapPut("", Write);
    }

    // Installed apps (App callers or calls through the app proxy) cannot change a person's preferences.
    internal static async Task<IResult> Write(Input? input, INeuronProxies brain, IBrainObserver kernel, CancellationToken ct)
    {
        if (BrainPreferenceStore.IsAppCaller()) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        var scope = BrainScope.CurrentId();
        if (!await BrainPreferenceStore.WriteNewTab(brain, kernel, scope, input?.NewTabApp, ct))
        { return Results.BadRequest(new { error = "Choose an installed app." }); }
        return Results.Ok(await Read(brain, kernel, scope, ct));
    }

    private static async Task<Preferences> Read(INeuronProxies brain, IBrainObserver kernel, string scope, CancellationToken ct)
    {
        var choices = await BrainPreferenceStore.NewTabChoices(brain, kernel, scope, ct);
        return new(await BrainPreferenceStore.ReadNewTab(brain, scope, choices, ct), choices);
    }
}
