using System.Net;
using IntoChat.OS.Modules.Microsoft.Aspire.Candidates;
using IntoChat.OS.Modules.Microsoft.Aspire.Deployment;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Tests;

// The cluster host keeps {Workdir}/live.json and drives the live AppHost through the aspire CLI.
public sealed class AspireCliClusterHostFacts : IDisposable
{
    private readonly string workdir = Directory.CreateTempSubdirectory("live-build-").FullName;

    public void Dispose() => Directory.Delete(workdir, recursive: true);

    [Fact]
    public async Task SetLiveRefusesABuildThatWasNotPublished()
    {
        var host = new AspireCliClusterHost(new RecordingRunner(""), _ => Task.FromResult(workdir));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => host.SetLive("missing", TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, await host.CurrentSha(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestartingRestartsTheMcpServersBeforeTheSilosAndTheSiloThatAskedLast()
    {
        var runner = new RecordingRunner("""
            {"resources":[
              {"name":"OperatingSystem-a-x1","displayName":"OperatingSystem-a"},{"name":"OperatingSystem-b-x2","displayName":"OperatingSystem-b"},
              {"name":"OperatingSystem-front-x3","displayName":"OperatingSystem-front"},
              {"name":"intochat-os-mcp-b-y1","displayName":"intochat-os-mcp-b"},{"name":"intochat-os-mcp-a-y2","displayName":"intochat-os-mcp-a"}]}
            """);
        var host = new AspireCliClusterHost(runner, _ => Task.FromResult(workdir), "OperatingSystem-a");

        await host.RestartSilos(TestContext.Current.CancellationToken);

        Assert.Equal([["describe", "--format", "Json"], ["resource", "intochat-os-mcp-a", "restart"], ["resource", "intochat-os-mcp-b", "restart"], ["resource", "OperatingSystem-b", "restart"]],
            runner.Calls.Select(call => call.Take(3).ToArray()));
        Assert.Equal([["resource", "OperatingSystem-a", "restart"]], runner.Detached.Select(call => call.Take(3).ToArray()));
    }

    [Fact]
    public async Task InstancesRollMcpServersFirstAndTheCallingSiloRestartsDetached()
    {
        var ct = TestContext.Current.CancellationToken;
        var runner = new RecordingRunner("""
            {"resources":[
              {"displayName":"OperatingSystem-b"},{"displayName":"OperatingSystem-a"},{"displayName":"OperatingSystem-front"},
              {"displayName":"intochat-os-mcp-b"},{"displayName":"intochat-os-mcp-a"}]}
            """);
        var host = new AspireCliClusterHost(runner, _ => Task.FromResult(workdir), "OperatingSystem-a");

        var instances = await host.Instances(ct);
        await host.RestartInstance("OperatingSystem-b", ct);
        await host.RestartInstance("OperatingSystem-a", ct);

        Assert.Equal(["intochat-os-mcp-a", "intochat-os-mcp-b", "OperatingSystem-a", "OperatingSystem-b"], instances);
        Assert.Equal(["resource", "OperatingSystem-b", "restart"], runner.Calls[^1].Take(3));
        Assert.Equal([["resource", "OperatingSystem-a", "restart"]], runner.Detached.Select(call => call.Take(3).ToArray()));
    }

    [Fact]
    public async Task AnInstanceThatExitsWhileStartingNeverBecomesReady()
    {
        var runner = new RecordingRunner("""{"resources":[{"displayName":"OperatingSystem-b","state":"Exited"}]}""");
        var host = new AspireCliClusterHost(runner, _ => Task.FromResult(workdir), "OperatingSystem-a");

        await Assert.ThrowsAsync<InstanceNeverReadyException>(() => host.WaitInstance("OperatingSystem-b", DateTimeOffset.MinValue, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    private static readonly Func<CancellationToken, Task<IReadOnlyDictionary<string, Uri>>> Fronts =
        _ => Task.FromResult<IReadOnlyDictionary<string, Uri>>(new Dictionary<string, Uri> { ["OperatingSystem"] = new("http://localhost:5080") });

    private const string TwoRunningSilos = """
        {"resources":[
          {"displayName":"OperatingSystem-a","state":"Running","urls":[{"name":"http","url":"http://localhost:5001"}]},
          {"displayName":"OperatingSystem-b","state":"Running","urls":[{"name":"http","url":"http://localhost:5002"}]}]}
        """;

    // Seen live: the instance's own port answered /ready while the front's tunnel to it was dead. Ready means the
    // front reaches it.
    [Fact]
    public async Task AnInstanceTheFrontCannotReachIsNotReadyThoughItsOwnPortAnswers()
    {
        var http = new Answers(uri => uri.Port == 5002 ? HttpStatusCode.OK : HttpStatusCode.BadGateway);
        var host = new AspireCliClusterHost(new RecordingRunner(TwoRunningSilos), _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, http);

        await Assert.ThrowsAsync<InstanceNeverReadyException>(() => host.WaitInstance("OperatingSystem-b", DateTimeOffset.MinValue, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.Contains(new Uri("http://localhost:5080/_instances/OperatingSystem-b/ready"), http.Asked);
        Assert.DoesNotContain(http.Asked, uri => uri.Port == 5002);
    }

    [Fact]
    public async Task AnInstanceIsReadyOnceTheFrontRoutesIt()
    {
        var http = new Answers(uri => uri.AbsolutePath == "/_instances/OperatingSystem-b/ready" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        var host = new AspireCliClusterHost(new RecordingRunner(TwoRunningSilos), _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, http);

        await host.WaitInstance("OperatingSystem-b", DateTimeOffset.MinValue, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DrainingPostsToTheInstancesOwnEndpoint()
    {
        var http = new Answers(_ => HttpStatusCode.Accepted);
        var host = new AspireCliClusterHost(new RecordingRunner(TwoRunningSilos), _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, http);

        await host.DrainInstance("OperatingSystem-b", TestContext.Current.CancellationToken);

        Assert.Equal([(HttpMethod.Post, new Uri("http://localhost:5002/drain"))], http.Requests);
    }

    [Fact]
    public async Task AnInstanceLeavesTheFrontOnlyWhileAnotherInstanceStillServes()
    {
        var ct = TestContext.Current.CancellationToken;
        var bothOut = new Answers(_ => HttpStatusCode.ServiceUnavailable);
        var peerServes = new Answers(uri => uri.AbsolutePath == "/_instances/OperatingSystem-a/ready" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);

        var refused = await Assert.ThrowsAsync<InstanceNeverReadyException>(() =>
            new AspireCliClusterHost(new RecordingRunner(TwoRunningSilos), _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, bothOut).WaitOutOfFront("OperatingSystem-b", TimeSpan.FromMinutes(1), ct));
        await new AspireCliClusterHost(new RecordingRunner(TwoRunningSilos), _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, peerServes).WaitOutOfFront("OperatingSystem-b", TimeSpan.FromMinutes(1), ct);

        Assert.Contains("no other OperatingSystem instance", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFrontsAreTheIntegrationsJson()
        => Assert.Equal(new Uri("http://localhost:5081"), AspireCliClusterHost.ParseFronts("""{"OperatingSystem":"http://localhost:5080","intochat-os-mcp":"http://localhost:5081"}""")["intochat-os-mcp"]);

    private sealed class Answers(Func<Uri, HttpStatusCode> answer) : HttpMessageHandler
    {
        public List<(HttpMethod, Uri)> Requests { get; } = [];
        public IEnumerable<Uri> Asked => Requests.Select(request => request.Item2);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) { Requests.Add((request.Method, request.RequestUri!)); }
            return Task.FromResult(new HttpResponseMessage(answer(request.RequestUri!)));
        }
    }

    // Seen live: a handover's wait read the old process's Finished between the restart's stop and its start.
    [Fact]
    public async Task TheOldProcessStoppingIsNotAFailedStart()
    {
        var restarted = DateTimeOffset.Parse("2026-10-10T12:54:43Z", System.Globalization.CultureInfo.InvariantCulture);
        var runner = new RecordingRunner("""{"resources":[{"displayName":"OperatingSystem-b","state":"Finished","startTimestamp":"2026-10-10T12:40:00Z"}]}""");
        var host = new AspireCliClusterHost(runner, _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, new Answers(_ => HttpStatusCode.OK));

        var waited = await Assert.ThrowsAsync<InstanceNeverReadyException>(() => host.WaitInstance("OperatingSystem-b", restarted, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        Assert.Contains("not ready within", waited.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProcessThatStartedAfterTheRestartAndStoppedNeverBecomesReady()
    {
        var restarted = DateTimeOffset.Parse("2026-10-10T12:54:43Z", System.Globalization.CultureInfo.InvariantCulture);
        var runner = new RecordingRunner("""{"resources":[{"displayName":"OperatingSystem-b","state":"Finished","startTimestamp":"2026-10-10T12:54:50Z"}]}""");
        var host = new AspireCliClusterHost(runner, _ => Task.FromResult(workdir), "OperatingSystem-a", Fronts, new Answers(_ => HttpStatusCode.OK));

        var waited = await Assert.ThrowsAsync<InstanceNeverReadyException>(() => host.WaitInstance("OperatingSystem-b", restarted, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));

        Assert.Contains("stopped (Finished)", waited.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInstanceIsReachedOnItsOwnHttpUrl()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""{"urls":[{"name":"https","url":"https://localhost:7001"},{"name":"http","url":"http://localhost:5001"}]}""");

        Assert.Equal(new Uri("http://localhost:5001"), AspireCliClusterHost.Endpoint(json.RootElement));
    }

    [Fact]
    public async Task SpansCountTheNamedSpansOfTheService()
    {
        var runner = new RecordingRunner("""
            [{"name":"features.load","source":"IntoChat.OS.Kernel"},{"name":"features.load","source":"IntoChat.OS.Kernel"},
             {"name":"features.load","source":"IntoChat.OS.Kernel-mcp"},{"name":"features.load","source":"IntoChat.OS.Mcp"},{"name":"other","source":"IntoChat.OS.Kernel"}]
            """);
        var host = new AspireCliClusterHost(runner, _ => Task.FromResult(workdir));

        Assert.Equal(3, await host.Spans("IntoChat.OS.Kernel", "features.load", TestContext.Current.CancellationToken));
        Assert.Equal(["otel", "spans", "--format", "Json", "--search", "features.load", "--limit", "10000"], runner.Calls.Single().Take(8));
    }

    [Fact]
    public void TheJsonStartsAtTheFirstLineThatOpensADocument()
        => Assert.Equal("[{\"a\":\"[x]\"}]", AspireCliClusterHost.JsonPart("info: started [ok]\n[{\"a\":\"[x]\"}]"));

    [Fact]
    public void OutputWithoutJsonIsAnError()
        => Assert.Throws<InvalidOperationException>(() => AspireCliClusterHost.JsonPart("Resource 'x' not found."));

    private sealed class RecordingRunner(string output) : IProcessRunner
    {
        public List<string[]> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(string file, string[] args, string workingDirectory, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
        {
            Calls.Add(args);
            return Task.FromResult(new ProcessResult(0, output, ""));
        }

        public List<string[]> Detached { get; } = [];

        public int Detach(string file, string[] args, string workingDirectory)
        {
            Detached.Add(args);
            return 1;
        }
    }
}
