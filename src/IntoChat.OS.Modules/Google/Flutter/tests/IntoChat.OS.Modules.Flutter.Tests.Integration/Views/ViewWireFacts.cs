using System.Text.Json;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Modules.Flutter.Views;

namespace IntoChat.OS.Modules.Flutter.Tests.Integration.Views;

public sealed class ViewWireFacts
{
    [Fact]
    public void MaximumDepthRoundTripsThroughRuntimeInvocationEnvelope()
    {
        var root = Ui.Text("leaf", "deep value");
        for (var i = 0; i < 31; i++) { root = Ui.Column("level" + i, [root]); }
        var view = new UiView(4, 7, root);
        var invocation = new RuntimeInvocation("behavior", typeof(IView).FullName!, "brain/apps/test/view", nameof(IView.Publish),
            [JsonSerializer.SerializeToElement(view, RuntimeProtocol.Json)]);
        var decoded = JsonSerializer.Deserialize<RuntimeInvocation>(JsonSerializer.Serialize(invocation, RuntimeProtocol.Json), RuntimeProtocol.Json)!;
        var result = decoded.Arguments[0].Deserialize<UiView>(RuntimeProtocol.Json)!;
        Assert.Equal((4L, 7L), (result.Generation, result.Version));
        var leaf = result.Root;
        for (var i = 0; i < 31; i++) { leaf = Assert.Single(leaf.Children); }
        Assert.Equal("deep value", leaf.Text);
    }
}
