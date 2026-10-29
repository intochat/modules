using System.Text.Json;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Flame;
using IntoChat.OS.Modules.Flame.Scene;
using IntoChat.OS.Modules.Flutter.Workspace;

namespace IntoChat.OS.Modules.Flutter.Space;

internal static class SpaceComposer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record SpaceEntry(string Key, string Title, string Description, AppPresentation? Presentation, AppToolbarEntry[] Toolbar);

    public static SceneDefinition Compose(WorkspaceState workspace, SpaceEntry[] field)
    {
        var brain = workspace.Path[0];
        var focus = workspace.Path[^1];
        var ordered = field.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
        List<SceneNode> nodes = [];
        var camera = new SceneCamera();
        for (var i = 0; i < ordered.Length; i++)
        {
            var entry = ordered[i];
            if (focus != brain && entry.Key != focus
                && !(focus.EndsWith("/profile", StringComparison.Ordinal) && CircleMembers.PostBrain(entry.Key) == focus[..^8])) { continue; }
            var angle = 2 * Math.PI * i / Math.Max(1, ordered.Length);
            var focused = entry.Key == focus;
            var transform = new Transform(320 * Math.Cos(angle), 320 * Math.Sin(angle), Scale: focused ? 2.2 : 1);
            nodes.Add(Node(entry, transform, focused));
            if (focused) { camera = new(transform.X, transform.Y, 1); }
        }
        if (focus != brain)
        { nodes.Add(Node(new(brain, "Home", "", null, []), new(), false)); }
        return new([.. nodes], camera);
    }

    private static SceneNode Node(SpaceEntry entry, Transform transform, bool focused)
    {
        var presentation = entry.Presentation;
        var root = presentation?.Root;
        var board = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in root?.Sphere?.BlackboardKeys ?? []) { board[key] = ""; }
        foreach (var key in FlameSchema.ReservedBlackboardKeys) { board[key] = "false"; }
        foreach (var (key, value) in presentation?.Blackboard ?? []) { board[key] = value; }
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icon"] = string.IsNullOrWhiteSpace(root?.Sphere?.Icon) ? "circle" : root.Sphere.Icon,
            ["blackboard"] = JsonSerializer.Serialize(board, Json),
            ["toolbar"] = JsonSerializer.Serialize(entry.Toolbar, Json),
            ["state"] = root is not null ? "ready" : presentation?.Error is not null ? "error" : "unready",
            ["neuron"] = entry.Key,
            ["focused"] = focused ? "true" : "false",
        };
        if (entry.Title.Length > 0) { parameters["title"] = entry.Title; }
        if (entry.Description.Length > 0) { parameters["description"] = entry.Description; }
        if (presentation?.Error is { Length: > 0 } error) { parameters["error"] = error; }
        if (focused && root?.Side is { } side) { parameters["side"] = JsonSerializer.Serialize(side, Json); }
        if (focused && root?.Main is { } main) { parameters["main"] = JsonSerializer.Serialize(main, Json); }
        return new(entry.Key, "sphere", transform, null, parameters, [], [], ["tap"], root?.Sphere?.Tree);
    }
}
