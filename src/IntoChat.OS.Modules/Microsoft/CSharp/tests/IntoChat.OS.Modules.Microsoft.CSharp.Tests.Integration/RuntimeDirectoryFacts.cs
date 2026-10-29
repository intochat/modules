using DigitalBrain.Capabilities;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class RuntimeDirectoryFacts
{
    [Fact]
    public void APublishedRuntimeAnswersIdentityOnlyForItsSpeakingSession()
    {
        var directory = new RuntimeDirectory();
        var speaking = Session(SessionStatus.Connected);
        directory.Publish("acc-brain/runtime", State(speaking));

        Assert.True(directory.TryIdentity("acc-brain/runtime", speaking.Id, "owner/synapses/a.cs", out var identity));
        Assert.Equal("owner", identity!.Owner);
        Assert.False(directory.TryIdentity("acc-brain/runtime", speaking.Id, "missing", out var missing));
        Assert.Null(missing);
        Assert.False(directory.TryIdentity("acc-brain/runtime", "other-session", "owner/synapses/a.cs", out _));

        directory.Publish("acc-brain/runtime", State(speaking with { Status = SessionStatus.Refused }));
        Assert.False(directory.TryIdentity("acc-brain/runtime", speaking.Id, "owner/synapses/a.cs", out _));
    }

    private static BrainRuntimeState State(SessionRecord session) => new()
    {
        Session = session,
        Programs = [ProgramAssignment.Of("owner", "hash", new Dictionary<string, string>(StringComparer.Ordinal), Principal(), [new("synapses/a.cs", "hash")])],
    };

    private static SessionRecord Session(SessionStatus status)
        => new("session-1", status, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static CapabilityCaller Principal() => new("principal", "account", "acc-brain");
}
