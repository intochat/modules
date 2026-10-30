using System.Text.Json;
using DigitalBrain.UI;
using IntoChat.OS.Modules.Flutter.Views;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class ViewPresenterFacts
{
    [Fact]
    public void MapsWebBrowserNodeToCardWithWebBrowserDetail()
    {
        var browser = new WebBrowser("studio-wb", "https://localhost:5000/app", "My Studio", 640, "wasm-ui");
        var mapped = ViewPresenter.Map("root", browser, "owner");

        Assert.Equal(UiNodeKind.Card, mapped.Kind);
        Assert.Equal("studio-wb", mapped.Key);
        Assert.Equal("My Studio", mapped.Text);
        Assert.NotNull(mapped.Detail);

        using var doc = JsonDocument.Parse(mapped.Detail!);
        var json = doc.RootElement;
        Assert.Equal("webbrowser", json.GetProperty("_ui").GetString());
        Assert.Equal("https://localhost:5000/app", json.GetProperty("url").GetString());
        Assert.Equal("My Studio", json.GetProperty("title").GetString());
        Assert.Equal(640.0, json.GetProperty("height").GetDouble());
        Assert.Equal("wasm-ui", json.GetProperty("executionMode").GetString());
    }

    [Fact]
    public void MapsBrainSceneNodeWithPauseRotationZoomTargetIdAndTargetZoom()
    {
        var scene = new BrainSceneNode(
            [new BrainGraphNode("n1", "App Node", "neuron://owner/app", "app", 1, 2, 3)],
            [new BrainGraphSynapse("n1", "n1", "Signal", true)],
            SelectedId: "n1")
        {
            PauseRotation = true,
            ZoomTargetId = "n1",
            TargetZoom = 2.25,
        };

        var mapped = ViewPresenter.Map("scene", scene, "owner");

        Assert.Equal(UiNodeKind.Card, mapped.Kind);
        Assert.Equal("scene", mapped.Key);
        Assert.NotNull(mapped.Detail);

        using var doc = JsonDocument.Parse(mapped.Detail!);
        var json = doc.RootElement;
        Assert.Equal("brain3d", json.GetProperty("_ui").GetString());
        Assert.Equal("n1", json.GetProperty("selectedId").GetString());
        Assert.True(json.GetProperty("pauseRotation").GetBoolean());
        Assert.Equal("n1", json.GetProperty("zoomTargetId").GetString());
        Assert.Equal(2.25, json.GetProperty("targetZoom").GetDouble());
    }
}
