using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Flutter.Chat;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IntoChat.OS.Modules.Flutter.Settings;

// Ino's chat list for Settings → Archived chats. Any member may list and unarchive. Titles are customer
// data, so installed apps and calls through the app proxy are refused on reads as well as writes.
internal static class ChatEndpoints
{
    internal sealed record Chat(string Id, string Title, DateTimeOffset UpdatedAt);

    internal sealed record Page(Chat[] Chats, int Total);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = BrainRoutes.Group(endpoints, "/chats");
        group.MapGet("", List);
        group.MapPost("/{id}/unarchive", Unarchive);
    }

    // Copy of Ino.DataKey in features/os/Ino/InoKeys.cs: the Ino app's ChatDataApp setting when set, else its own
    // key, plus "/chats". ChatEndpointFacts pins the rule.
    internal static string IndexKey(string brain, AppSnapshot ino)
    {
        ArgumentNullException.ThrowIfNull(ino);
        var data = ino.Settings.GetValueOrDefault("ChatDataApp");
        return (data is { Length: > 0 } ? data : IApp.Key(brain, "ino")) + "/chats";
    }

    internal static async Task<IResult> List(INeuronProxies brain, CancellationToken ct, bool archived = true, int skip = 0, int take = ChatIndex.MaxPage)
    {
        if (Refused()) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        if (skip < 0 || take is < 1 or > ChatIndex.MaxPage) { return Results.BadRequest(new { error = "Page out of range." }); }
        var page = await (await Index(brain, ct)).List(archived, skip, take).WaitAsync(ct);
        return Results.Ok(new Page([.. page.Entries.Select(entry => new Chat(entry.Id, entry.Title, entry.UpdatedAt))], page.Total));
    }

    internal static async Task<IResult> Unarchive(string id, INeuronProxies brain, CancellationToken ct)
    {
        if (Refused()) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        var index = await Index(brain, ct);
        if (!ChatIndex.IsId(id) || await index.Get(id).WaitAsync(ct) is null)
        { return Results.NotFound(new { error = "That chat no longer exists." }); }
        await index.Unarchive(id).WaitAsync(ct);
        return Results.NoContent();
    }

    private static bool Refused()
        => CallerContextStamper.TryGet(out var caller) && (caller.Kind == CallerKind.App || caller.StampedBy == TrustedEdge.AppProxy);

    private static async Task<IChatIndex> Index(INeuronProxies brain, CancellationToken ct)
    {
        var brainId = BrainScope.CurrentId();
        var ino = await brain.Get<IApp>(IApp.Key(brainId, "ino")).Read().WaitAsync(ct);
        return brain.Get<IChatIndex>(IndexKey(brainId, ino));
    }
}
