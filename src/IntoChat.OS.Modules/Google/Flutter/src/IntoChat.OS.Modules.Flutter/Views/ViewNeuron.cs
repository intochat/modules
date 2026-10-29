using DigitalBrain;
using System.Buffers;
using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Views;

[Neuron(UIVocabulary.ViewType)]
internal sealed class ViewNeuron
    : UiNeuron<ViewState>, IView
{
    public Task Publish(UiView view)
    {
        RequireWriter();
        var next = ViewValidation.Clone(view, FullKey);
        if (Snapshot.View is { } current)
        {
            if (next.Generation < current.Generation || next.Generation == current.Generation && next.Version < current.Version) { return Task.CompletedTask; }
            if (next.Generation == current.Generation && next.Version == current.Version)
            {
                if (JsonSerializer.Serialize(next, ViewValidation.Json) != JsonSerializer.Serialize(current, ViewValidation.Json)) { throw new InvalidOperationException("A published version cannot change."); }
                return Task.CompletedTask;
            }
        }
        return Save(new() { View = next, Changes = Snapshot.Changes + 1 }, new ViewChanged(FullKey, next.Generation, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<UiView?> Read() => Task.FromResult(Snapshot.View is { } view ? ViewValidation.Clone(view, FullKey) : null);

    public Task Activate(long generation, string key, string expectedAction, Guid requestId)
    {
        if (requestId == Guid.Empty) { throw new ArgumentException("A request id is required.", nameof(requestId)); }
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAction);
        var view = Snapshot.View;
        var node = view is null ? null : Find(view.Root, key);
        if (view?.Generation != generation || node is not { Kind: UiNodeKind.Button, Enabled: true } || !string.Equals(node.Action, expectedAction, StringComparison.Ordinal))
        { throw new StaleActionException("This action is no longer available. Read the view again."); }
        return Publish(new ViewAction(FullKey, node.Key, node.Action!, requestId, view.Generation));
    }

    private static UiNode? Find(UiNode root, string key) => root.Key == key ? root : root.Children.Select(child => Find(child, key)).FirstOrDefault(node => node is not null);

    private void RequireWriter()
    {
        if (!CallerContextStamper.TryGet(out var caller) || caller.Kind == CallerKind.Platform) { return; }
        var name = FullKey;
        if (!BrainKey.BelongsTo(name, caller.BrainId) || caller.Kind == CallerKind.App &&
            (string.IsNullOrEmpty(caller.AppId) || !name.StartsWith(caller.AppId + "/", StringComparison.Ordinal)))
        { throw new UnauthorizedAccessException("Only the owning brain and app may publish this view."); }
    }
}

[GenerateSerializer, Alias("ui.view-state")]
[ExportableState]
internal sealed class ViewState
{
    [Id(0)] public UiView? View { get; set; }
    // Publications so far: the retained ViewChanged's sequence (a view saved before this counted none).
    [Id(1)] public long Changes { get; set; }
}

internal static class ViewValidation
{
    internal static readonly JsonSerializerOptions Json = new() { MaxDepth = 128 };
    // Only leaf controls can be embedded: layouts and views can themselves refer to other nodes.
    // A viewport is the one window: it shows an app's view chosen by its owner (the desktop's windows).
    // Line and paragraph separators and bidirectional controls: they hide what a copied link says.
    private static readonly SearchValues<char> HiddenDirection = SearchValues.Create("\u2028\u2029\u200e\u200f\u202a\u202b\u202c\u202d\u202e\u2066\u2067\u2068\u2069");

    private static readonly HashSet<string> ReferenceKinds = new(StringComparer.Ordinal)
    { UIVocabulary.ConfirmationType, "viewport", "chat", "composer", "text", "button", "textfield", "select", "fileinput", "imagecanvas", "form", "table", "chart", "voiceinput", "webbrowser", "toggle", "progress", "infobar", "image", "entrycontrol", "preference" };

    public static UiView Clone(UiView view, string name)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Generation < 0 || view.Version < 1) { throw new ArgumentException("Generation must be nonnegative and version positive."); }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var path = new HashSet<UiNode>(ReferenceEqualityComparer.Instance);
        var count = 0;
        var copy = view with { Root = Copy(view.Root, 1) };
        if (JsonSerializer.SerializeToUtf8Bytes(copy, Json).Length > 1_048_576) { throw new ArgumentException("A view is limited to 1 MiB."); }
        return copy;

        static bool IsButtonReference(UiNode? node) => node is { Kind: UiNodeKind.Reference, Reference.Kind: UIVocabulary.ButtonType };

        UiNode Copy(UiNode node, int depth)
        {
            if (node is null || depth > 32 || ++count > 512 || !path.Add(node)) { throw new ArgumentException("A view must be an acyclic tree of at most 512 nodes and depth 32."); }
            if (!Enum.IsDefined(node.Kind) || string.IsNullOrWhiteSpace(node.Key) || node.Key.Length > 256 || !keys.Add(node.Key)) { throw new ArgumentException("View nodes require unique nonempty keys and known kinds."); }
            if (node.Children is null || !double.IsFinite(node.Gap) || node.Gap < 0) { throw new ArgumentException("Invalid view layout."); }
            if (node.Extents is { } extents && (extents.Length != node.Children.Length || extents.Any(value => !double.IsFinite(value) || value < 0))) { throw new ArgumentException("Extents must match children and be finite nonnegative values."); }
            if (node.Kind is UiNodeKind.Text or UiNodeKind.Button or UiNodeKind.Reference or UiNodeKind.Badge or UiNodeKind.Avatar or UiNodeKind.Divider && node.Children.Length != 0) { throw new ArgumentException("Leaf nodes cannot have children."); }
            if (!Enum.IsDefined(node.Style) || !Enum.IsDefined(node.Tone)) { throw new ArgumentException("View nodes require a known style and tone."); }
            if (node.Icon is { } icon && (icon.Length is 0 or > 64 || !icon.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))) { throw new ArgumentException("Icons are at most 64 characters of [a-z0-9-]."); }
            if (node.Kind is UiNodeKind.Badge or UiNodeKind.Avatar && string.IsNullOrWhiteSpace(node.Text)) { throw new ArgumentException("Badges and avatars require text."); }
            // A menu item is a button, or a button neuron referenced so it keeps its own activation.
            if (node.Kind == UiNodeKind.Menu && (node.Children.Any(child => child?.Kind is not (UiNodeKind.Button or UiNodeKind.Divider or UiNodeKind.Text) && !IsButtonReference(child))
                || !node.Children.Any(child => child.Kind == UiNodeKind.Button || IsButtonReference(child))))
            { throw new ArgumentException("Menus hold buttons, button references, dividers and text labels, with at least one button."); }
            if (node.Kind == UiNodeKind.Tabs && (node.Text is not null || node.Children.Length == 0 || node.Children.Any(child => child?.Kind != UiNodeKind.Button) || node.Children.Count(child => child.Style == UiStyle.Primary) != 1))
            { throw new ArgumentException("Tabs hold buttons, exactly one of them active."); }
            if (node.Kind == UiNodeKind.Modal && (string.IsNullOrWhiteSpace(node.Text) || !node.Children.Any(child => child?.Kind == UiNodeKind.Button)))
            { throw new ArgumentException("Modals require a title and a button that closes or confirms them."); }
            if (node.Kind == UiNodeKind.Toast && node.Children.Count(child => child?.Kind == UiNodeKind.Button) > 1) { throw new ArgumentException("A toast has at most one button."); }
            if (node.Kind == UiNodeKind.Button && (string.IsNullOrWhiteSpace(node.Text) || string.IsNullOrWhiteSpace(node.Action))) { throw new ArgumentException("Buttons require text and a canonical action."); }
            if (node.Action is { } action && action.StartsWith("copy:", StringComparison.Ordinal) && (action.Length is 5 or > 2053 || action.AsSpan(5).ContainsAnyInRange('\u0000', '\u001f') || action.AsSpan(5).ContainsAnyInRange('\u007f', '\u009f') || action.AsSpan(5).IndexOfAny(HiddenDirection) >= 0))
            { throw new ArgumentException("Copy actions carry 1 to 2048 printable characters."); }
            if (node.Kind != UiNodeKind.Button && node.Action is not null) { throw new ArgumentException("Only buttons have actions."); }
            if (node.Kind == UiNodeKind.Reference)
            {
                if (node.Reference is not { } reference || !ReferenceKinds.Contains(reference.Kind) || string.IsNullOrWhiteSpace(reference.Name) || !BrainKey.BelongsTo(reference.Name, BrainKey.BrainOf(name)))
                { throw new ArgumentException("References must name a supported leaf control in the same brain."); }
            }
            else if (node.Reference is not null) { throw new ArgumentException("Only reference nodes may embed controls."); }
            var result = node with { Children = node.Children.Select(child => Copy(child, depth + 1)).ToArray(), Extents = node.Extents?.ToArray(), Reference = node.Reference is { } childRef ? childRef with { } : null };
            path.Remove(node);
            return result;
        }
    }
}
