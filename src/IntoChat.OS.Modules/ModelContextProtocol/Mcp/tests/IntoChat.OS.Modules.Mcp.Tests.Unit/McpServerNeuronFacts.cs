using DigitalBrain.Spi;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.Tools;

namespace IntoChat.OS.Modules.Mcp.Tests.Unit;

// The neuron's argument and state rules, answered before any session is opened: no server, no socket.
public sealed class McpServerNeuronFacts
{
    // A plain neuron on a fake context (06-06d), its state seeded before it activates.
    private static McpServerNeuron Neuron(McpServerState? state = null)
    {
        var neuron = new McpServerNeuron();
        var context = new FakeNeuronContext(DigitalBrain.NeuronId.Parse("facts/mcp.server/mcp/fake"));
        if (state is not null) { context.MemoryState.Save(new McpServerStore { State = state }, []).GetAwaiter().GetResult(); }
        NeuronAttachment.Attach(neuron, context);
        NeuronAttachment.Activate(neuron, CancellationToken.None).GetAwaiter().GetResult();
        return neuron;
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.test/mcp")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:secret@example.test/mcp")]
    public async Task ConnectRefusesAUrlThatIsNotCredentialFreeHttp(string url)
        => await Assert.ThrowsAsync<ArgumentException>(() => Neuron().Connect(new McpServerSpec("Title", url), TestContext.Current.CancellationToken));

    [Fact]
    public async Task ConnectRefusesABlankTitleAndANullSpec()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Neuron().Connect(new McpServerSpec(" ", "https://example.test/mcp"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Neuron().Connect(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ANeverConnectedServerReadsEmptyAndOffersNoTools()
    {
        var neuron = Neuron();

        var state = await neuron.Read();
        var tools = await neuron.Tools(TestContext.Current.CancellationToken);

        Assert.False(state.Connected);
        Assert.Empty(state.Tools);
        Assert.Empty(tools.Tools);
        Assert.Equal("", tools.Title);
    }

    [Fact]
    public async Task ADisconnectedServerKeepsItsStateButOffersNoTools()
    {
        var tool = new ToolDescriptor("echo", "Echo", "", "{\"type\":\"object\"}", true);
        var neuron = Neuron(new McpServerState("Fake", "https://example.test/mcp", false, [tool], null, null));

        Assert.Equal("Fake", (await neuron.Read()).Title);
        Assert.Empty((await neuron.Tools(TestContext.Current.CancellationToken)).Tools);
    }

    [Fact]
    public async Task AConnectedServerOffersItsToolsUnderItsTitle()
    {
        var tool = new ToolDescriptor("echo", "Echo", "", "{\"type\":\"object\"}", true);
        var neuron = Neuron(new McpServerState("Fake", "https://example.test/mcp", true, [tool], null, null));

        var tools = await neuron.Tools(TestContext.Current.CancellationToken);

        Assert.Equal("Fake", tools.Title);
        Assert.Equal("echo", Assert.Single(tools.Tools).Name);
    }

    [Fact]
    public async Task RunNeedsAToolNameAndAConnectedServer()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Neuron().Run(" ", "{}", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Neuron().Run("echo", "{}", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("{broken")]
    public async Task RunRefusesArgumentsThatAreNotAJsonObject(string arguments)
    {
        var connected = new McpServerState("Fake", "https://example.test/mcp", true, [], null, null);
        await Assert.ThrowsAsync<ArgumentException>(() => Neuron(connected).Run("echo", arguments, TestContext.Current.CancellationToken));
    }
}
