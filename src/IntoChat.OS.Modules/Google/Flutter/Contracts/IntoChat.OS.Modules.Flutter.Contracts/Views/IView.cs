using DigitalBrain.Contracts;
using DigitalBrain;


namespace IntoChat.OS.Modules.Flutter.Views;

[NeuronAlias("view"), DefaultNeuronType(UIVocabulary.ViewType)]
public interface IView
{
    Task Publish(UiView view);
    [DigitalBrain.ReadOnlyCall] Task<UiView?> Read();
    Task Activate(long generation, string key, string expectedAction, Guid requestId);
}

public sealed record UiView(long Generation, long Version, UiNode Root);

public enum UiNodeKind { Text = 0, Button = 1, Column = 2, Row = 3, Card = 4, Reference = 5, Menu = 6, Modal = 7, Toast = 8, List = 9, Badge = 10, Avatar = 11, Divider = 12, Tabs = 13 }

public enum UiStyle { Primary = 0, Secondary = 1, Ghost = 2, Danger = 3, Link = 4 }

public enum UiTone { Neutral = 0, Done = 1, Working = 2, Error = 3 }

public sealed record UiNode(UiNodeKind Kind, string Key)
{
    public string? Text { get; init; }
    public string? Detail { get; init; }
    public string? Action { get; init; }
    public bool Enabled { get; init; } = true;
    public UiNode[] Children { get; init; } = [];
    public double[]? Extents { get; init; }
    public UiChildRef? Reference { get; init; }
    public double Gap { get; init; } = 12;
    public bool FollowEnd { get; init; }
    public string? Icon { get; init; }
    public UiStyle Style { get; init; }
    public UiTone Tone { get; init; }
}

public static class Ui
{
    public static UiNode Text(string key, string text) => new(UiNodeKind.Text, key) { Text = text };
    public static UiNode Button(string key, string text, string action, bool enabled = true, UiStyle style = UiStyle.Secondary, string? icon = null) => new(UiNodeKind.Button, key) { Text = text, Action = action, Enabled = enabled, Style = style, Icon = icon };
    // The renderer copies the text and shows a toast; no view action is dispatched.
    public static UiNode CopyButton(string key, string label, string text) => Button(key, label, "copy:" + text, icon: "copy");
    // Text is the accessible label; the shell shows only the icon.
    public static UiNode IconButton(string key, string icon, string label, string action) => new(UiNodeKind.Button, key) { Text = label, Action = action, Style = UiStyle.Ghost, Icon = icon };
    public static UiNode Column(string key, UiNode[] children, double gap = 12, double[]? extents = null, bool followEnd = false) => new(UiNodeKind.Column, key) { Children = children, Gap = gap, Extents = extents, FollowEnd = followEnd };
    public static UiNode Row(string key, UiNode[] children, double gap = 12, double[]? extents = null) => new(UiNodeKind.Row, key) { Children = children, Gap = gap, Extents = extents };
    public static UiNode Card(string key, string text, string? detail = null, UiNode[]? children = null) => new(UiNodeKind.Card, key) { Text = text, Detail = detail, Children = children ?? [] };
    public static UiNode Reference(string key, UiChildRef reference) => new(UiNodeKind.Reference, key) { Reference = reference };
    public static UiNode Menu(string key, string label, UiNode[] items, string? icon = null) => new(UiNodeKind.Menu, key) { Text = label, Children = items, Icon = icon };
    public static UiNode Modal(string key, string title, string? detail, UiNode[] children) => new(UiNodeKind.Modal, key) { Text = title, Detail = detail, Children = children };
    public static UiNode Toast(string key, string text, UiTone tone = UiTone.Neutral, UiNode? action = null) => new(UiNodeKind.Toast, key) { Text = text, Tone = tone, Children = action is null ? [] : [action] };
    public static UiNode List(string key, UiNode[] rows) => new(UiNodeKind.List, key) { Children = rows };
    public static UiNode Badge(string key, string text, UiTone tone) => new(UiNodeKind.Badge, key) { Text = text, Tone = tone };
    public static UiNode Avatar(string key, string initials) => new(UiNodeKind.Avatar, key) { Text = initials };
    public static UiNode Divider(string key, string? label = null) => new(UiNodeKind.Divider, key) { Text = label };
    public static UiNode Tabs(string key, UiNode[] tabs) => new(UiNodeKind.Tabs, key) { Children = tabs };
    public static UiNode Tab(string key, string label, string action, bool active) => new(UiNodeKind.Button, key) { Text = label, Action = action, Style = active ? UiStyle.Primary : UiStyle.Ghost };
}

[Signal("ui.view-changed")]
public sealed record ViewChanged(string Name, long Generation, long Version) : DigitalBrain.Signal;

[Signal("ui.view-action")]
public sealed record ViewAction(string Name, string Key, string Action, Guid RequestId, long Generation) : DigitalBrain.Signal;
