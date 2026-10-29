using System.Text.Json;
using IntoChat.OS.Modules.Flutter.Views;
using Xunit;

namespace IntoChat.OS.Modules.Flutter.Tests.Integration.Views;

public sealed class ViewValidationFacts
{
    [Fact]
    public void SnapshotClonesEveryMutableArray()
    {
        var children = new[] { Ui.Text("text", "original") };
        var extents = new[] { 40d };
        var input = new UiView(1, 1, Ui.Column("root", children, extents: extents));
        var stored = ViewValidation.Clone(input, "brain/apps/test/view");
        children[0] = Ui.Text("text", "mutated");
        extents[0] = 500;
        var read = ViewValidation.Clone(stored, "brain/apps/test/view");
        read.Root.Children[0] = Ui.Text("text", "mutated read");
        read.Root.Extents![0] = 900;
        Assert.Equal("original", stored.Root.Children[0].Text);
        Assert.Equal(40, stored.Root.Extents![0]);
    }

    [Fact]
    public void RejectsCyclesDuplicatesAndTreeBounds()
    {
        var cyclic = Ui.Column("cycle", new UiNode[1]);
        cyclic.Children[0] = cyclic;
        Invalid(cyclic);
        Invalid(Ui.Column("root", [Ui.Text("duplicate", "a"), Ui.Text("duplicate", "b")]));
        Invalid(Ui.Column("root", Enumerable.Range(0, 512).Select(i => Ui.Text(i.ToString(), "x")).ToArray()));
        var deep = Ui.Text("leaf", "x");
        for (var i = 0; i < 32; i++) { deep = Ui.Column("level" + i, [deep]); }
        Invalid(deep);
        Invalid(Ui.Text("large", new string('x', 1_048_576)));
        Invalid(Ui.Row("bad-extents", [Ui.Text("text", "x")], extents: [double.NaN]));
        Invalid(new((UiNodeKind)90, "unknown"));
        Invalid(Ui.Text(" ", "empty key"));
    }

    [Fact]
    public void ReferencesAreSameBrainAndCannotRecurseThroughCompositeControls()
    {
        var valid = Ui.Reference("chat", new("chat", "brain/apps/test/chat"));
        Assert.Equal(valid.Reference, ViewValidation.Clone(new(1, 1, valid), "brain/apps/test/view").Root.Reference);
        Invalid(Ui.Reference("other", new("chat", "other/apps/test/chat")));
        Invalid(Ui.Reference("prefix", new("chat", "brain-other/apps/test/chat")));
        foreach (var kind in new[] { "toggle", "progress", "infobar", "image", "viewport" })
        { Assert.Equal(kind, ViewValidation.Clone(new(1, 1, Ui.Reference(kind, new(kind, "brain/apps/test/" + kind))), "brain/apps/test/view").Root.Reference!.Kind); }
        foreach (var kind in new[] { "view", "layout", "surface", "card", "collection", "tabs", "expander", "unknown" })
        { Invalid(Ui.Reference(kind, new(kind, "brain/apps/test/child"))); }
    }

    [Fact]
    public void EveryKitBuilderRoundTrips()
    {
        var root = Ui.Column("root",
        [
            Ui.Button("save", "Save", "save", style: UiStyle.Secondary, icon: "save"),
            Ui.IconButton("close", "x", "Close", "close"),
            Ui.Menu("menu", "More", [Ui.Text("label", "Actions"), Ui.Button("rename", "Rename", "rename"), Ui.Divider("split"), Ui.Button("delete", "Delete", "delete", style: UiStyle.Danger)], icon: "ellipsis"),
            Ui.Modal("modal", "Delete app?", "This cannot be undone.", [Ui.Button("cancel", "Cancel", "cancel", style: UiStyle.Ghost), Ui.Button("confirm", "Delete", "confirm", style: UiStyle.Danger)]),
            Ui.Toast("toast", "Saved", UiTone.Done, Ui.Button("undo", "Undo", "undo", style: UiStyle.Link)),
            Ui.Toast("plain", "Working", UiTone.Working),
            Ui.List("list", [Ui.Row("row", [Ui.Avatar("avatar", "VH"), Ui.Text("name", "Vlad"), Ui.Badge("badge", "Error", UiTone.Error)])]),
            Ui.Divider("labelled", "Today"),
        ]);
        var copy = ViewValidation.Clone(new(1, 1, root), "brain/apps/test/view").Root;
        Assert.Equal(JsonSerializer.Serialize(root, ViewValidation.Json), JsonSerializer.Serialize(copy, ViewValidation.Json));
        Assert.Equal((UiStyle.Ghost, "x", "Close"), (copy.Children[1].Style, copy.Children[1].Icon, copy.Children[1].Text));
        Assert.Equal(UiTone.Done, copy.Children[4].Tone);
    }

    [Fact]
    public void KitContainersEnforceTheirShape()
    {
        Invalid(Ui.Menu("menu", "More", [Ui.Text("label", "Only a label"), Ui.Divider("split")]));
        Invalid(Ui.Menu("menu", "More", [Ui.Button("go", "Go", "go"), Ui.Column("nested", [])]));
        Invalid(Ui.Modal("modal", " ", null, [Ui.Button("ok", "OK", "ok")]));
        Invalid(Ui.Modal("modal", "Title", null, [Ui.Text("body", "No button")]));
        Invalid(new(UiNodeKind.Toast, "toast") { Text = "Two", Children = [Ui.Button("a", "A", "a"), Ui.Button("b", "B", "b")] });
        Invalid(Ui.Badge("badge", " ", UiTone.Done));
        Invalid(Ui.Avatar("avatar", ""));
        Invalid(Ui.Divider("divider") with { Children = [Ui.Text("text", "x")] });
        Invalid(Ui.Badge("badge", "x", UiTone.Done) with { Children = [Ui.Text("text", "x")] });
        Invalid(Ui.Menu("menu", "More", [Ui.Button("go", "Go", "go")]) with { Action = "open" });
    }

    // A menu item may be a button neuron, so it keeps its own activation (the desktop's narrow More).
    [Fact]
    public void MenusMayHoldButtonNeuronReferences()
    {
        var settings = Ui.Reference("settings", new(UIVocabulary.ButtonType, "brain/apps/test/settings"));
        var menu = Ui.Menu("menu", "More", [Ui.Text("usage", "$1.00"), settings], icon: "more-horizontal");
        Assert.Equal(UiNodeKind.Reference, ViewValidation.Clone(new(1, 1, menu), "brain/apps/test/view").Root.Children[1].Kind);
        Invalid(Ui.Menu("menu", "More", [Ui.Reference("viewport", new(UIVocabulary.ViewportType, "brain/apps/test/window"))]));
        Invalid(Ui.Menu("menu", "More", [Ui.Button("go", "Go", "go"), Ui.Reference("text", new("text", "brain/apps/test/text"))]));
        Invalid(Ui.Menu("menu", "More", [Ui.Reference("foreign", new(UIVocabulary.ButtonType, "other/apps/test/settings"))]));
    }

    [Fact]
    public void IconsStyleAndToneAreBounded()
    {
        Invalid(Ui.Button("go", "Go", "go", icon: "../x"));
        Invalid(Ui.Button("go", "Go", "go", icon: "Upper"));
        Invalid(Ui.Button("go", "Go", "go", icon: ""));
        Invalid(Ui.Button("go", "Go", "go", icon: new string('a', 65)));
        Assert.Equal(64, ViewValidation.Clone(new(1, 1, Ui.Button("go", "Go", "go", icon: new string('a', 64))), "brain/apps/test/view").Root.Icon!.Length);
        Invalid(Ui.Button("go", "Go", "go", style: (UiStyle)9));
        Invalid(Ui.Badge("badge", "x", (UiTone)9));
    }

    [Fact]
    public void TabsHoldOnlyButtonsWithExactlyOneActive()
    {
        var tabs = Ui.Tabs("tabs", [Ui.Tab("a", "A", "open-a", true), Ui.Tab("b", "B", "open-b", false)]);
        var copy = ViewValidation.Clone(new(1, 1, tabs), "brain/apps/test/view").Root;
        Assert.Equal((UiNodeKind.Tabs, UiStyle.Primary, UiStyle.Ghost), (copy.Kind, copy.Children[0].Style, copy.Children[1].Style));
        InvalidTabs(Ui.Tabs("tabs", []));
        InvalidTabs(Ui.Tabs("tabs", [Ui.Tab("a", "A", "open-a", true), Ui.Text("text", "x")]));
        InvalidTabs(Ui.Tabs("tabs", [Ui.Tab("a", "A", "open-a", true), Ui.Tab("b", "B", "open-b", true)]));
        InvalidTabs(Ui.Tabs("tabs", [Ui.Tab("a", "A", "open-a", false), Ui.Tab("b", "B", "open-b", false)]));
        InvalidTabs(tabs with { Text = "Tabs" });
    }

    [Fact]
    public void CopyActionsAreBoundedAndPrintable()
    {
        var button = Ui.CopyButton("share", "Copy link", "https://example.test/#/open?app=fixture-value-0001");
        Assert.Equal("copy:https://example.test/#/open?app=fixture-value-0001", button.Action);
        Assert.Equal("copy", button.Icon);
        Assert.Equal(UiStyle.Secondary, button.Style);
        Assert.Equal(button.Action, ViewValidation.Clone(new(1, 1, button), "brain/apps/test/view").Root.Action);
        Assert.Equal(new string('x', 2048), ViewValidation.Clone(new(1, 1, Ui.CopyButton("max", "Copy", new string('x', 2048))), "brain/apps/test/view").Root.Action![5..]);
        foreach (var text in new[] { "", new string('x', 2049), "line\nbreak", "tab\tbed", "bell\u0007", "sep\u2028x", "para\u2029x", "lrm\u200ex", "rlm\u200fx", "emb\u202ax", "pdf\u202cx", "ovr\u202ex", "iso\u2066x", "pdi\u2069x" })
        { InvalidCopy(Ui.Button("copy", "Copy", "copy:" + text)); }
    }

    private static void InvalidCopy(UiNode root) => Assert.Equal("Copy actions carry 1 to 2048 printable characters.", Assert.Throws<ArgumentException>(() => ViewValidation.Clone(new(1, 1, root), "brain/apps/test/view")).Message);

    private static void InvalidTabs(UiNode root) => Assert.Equal("Tabs hold buttons, exactly one of them active.", Assert.Throws<ArgumentException>(() => ViewValidation.Clone(new(1, 1, root), "brain/apps/test/view")).Message);

    private static void Invalid(UiNode root) => Assert.Throws<ArgumentException>(() => ViewValidation.Clone(new(1, 1, root), "brain/apps/test/view"));
}
