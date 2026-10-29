using System.Text.Json;
using DigitalBrain.Runtime.Orleans.Signalling;
using IntoChat.OS.Modules.Flutter.Workspace;

namespace IntoChat.OS.Modules.Flutter.Tests.Integration.Workspace;

public sealed class LiveSignalEndpointsFacts
{
    // The golden bytes are shared with the Dart mirror's check; a change here is a wire change. Every live feed is a plain
    // publisher's (06-08), so the envelope is PlainEnvelope's: the payload is the [Signal] record alone.
    [Fact]
    public async Task TheEnvelopeShapeIsUnchanged()
    {
        var publisher = DigitalBrain.NeuronId.Parse("brain/chats.chat/chat");
        using var payload = JsonDocument.Parse("""{"name":"brain/chat","revision":7}""");
        var entry = new SessionSignal(publisher.ToString(), 42, "test.live-envelope-chat-changed", payload.RootElement.Clone(), DateTimeOffset.UnixEpoch);
        var golden = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Contracts", "live-envelope.golden.json"), TestContext.Current.CancellationToken);

        Assert.Equal(golden, LiveSignalEndpoints.PlainEnvelope(typeof(ChatChanged), publisher, entry, replay: true));
    }
}

// A [Signal] record of the shape the golden envelope was captured with.
[DigitalBrain.Signal("test.live-envelope-chat-changed")]
public sealed record ChatChanged(string Name, long Revision) : DigitalBrain.Signal;
