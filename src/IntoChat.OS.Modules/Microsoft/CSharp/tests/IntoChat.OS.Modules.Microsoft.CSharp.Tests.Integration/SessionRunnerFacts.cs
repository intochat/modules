using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class SessionRunnerFacts
{
    [Fact]
    public async Task EnsurePostsTheSessionWithItsTokenAndEdge()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new FakeSessionHost();
        var runner = Runner(host);

        await runner.Ensure(new("acc-brain", "s1", "tok", "http://edge.test/"), ct);

        var start = Assert.Single(host.Requests, FakeSessionHost.IsStart);
        Assert.Equal("sessions/" + SessionRunner.Identifier("acc-brain"), start.Path);
        Assert.Equal("tok", start.Body!["token"]!.GetValue<string>());
        Assert.Equal("http://edge.test/", start.Body!["edgeUrl"]!.GetValue<string>());
        Assert.True(await runner.Exists("acc-brain", ct));
        await runner.Stop("acc-brain", ct);
        Assert.False(await runner.Exists("acc-brain", ct));
    }

    [Fact]
    public async Task StoppingASessionTheHostNeverHadIsQuiet()
        => await Runner(new FakeSessionHost()).Stop("acc-brain", TestContext.Current.CancellationToken);

    [Fact]
    public void ThePoolIdentifierNeverLeaksTheBrainId()
        => Assert.DoesNotContain("acc-brain", SessionRunner.Identifier("acc-brain"), StringComparison.Ordinal);

    private static SessionRunner Runner(FakeSessionHost host)
        => new(new HttpClient(host), Options.Create(new CSharpOptions { SandboxUrl = FakeSessionHost.Url }));
}
