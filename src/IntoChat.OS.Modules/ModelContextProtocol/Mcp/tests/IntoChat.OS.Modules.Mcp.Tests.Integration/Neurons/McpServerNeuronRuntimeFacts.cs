using DigitalBrain.Testing;

namespace IntoChat.OS.Modules.Mcp.Tests.Integration;

// The plain MCP server neuron (06-06d) on both runtimes (R4-05): its state rules and what a failed connect saves and publishes.
// The live server path (tools, search, Run) is proven against a real MCP server in McpServerFacts.
public sealed class McpServerNeuronRuntimeFacts(McpFixture fixture) : IClassFixture<McpFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => McpFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task ANeverConnectedServerReadsEmptyAndOffersNoTools(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var server = runtime.Get<IMcpServer>(runtime.Id("mcp.server", $"mcp/empty-{kind}"));

        Assert.False((await server.Read()).Connected);
        Assert.Empty((await server.Tools(TestContext.Current.CancellationToken)).Tools);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnUnreachableServerIsSavedDisconnectedWithItsErrorAndTheChangeIsPublished(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("mcp.server", $"mcp/unreachable-{kind}");
        var server = runtime.Get<IMcpServer>(id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => server.Connect(new McpServerSpec("Gone", "http://127.0.0.1:9/mcp"), ct));

        var state = await server.Read();
        Assert.Equal(("Gone", false), (state.Title, state.Connected));
        Assert.False(string.IsNullOrEmpty(state.Error));
        var changed = Assert.Single(await runtime.WaitForSignals<McpServerChanged>(id, 1, TimeSpan.FromSeconds(20), ct));
        Assert.Equal("Gone", changed.State.Title);
    }
}
