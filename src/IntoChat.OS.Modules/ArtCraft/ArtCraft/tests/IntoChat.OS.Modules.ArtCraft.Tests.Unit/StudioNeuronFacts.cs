using DigitalBrain.Testing;

namespace IntoChat.OS.Modules.ArtCraft.Tests;

// The plain studio neuron (06-06a). The Unit tier runs each fact on TestOS; the Integration tier compiles this same file and runs
// each on TestOS and on SiloHost.Local (R4-05). Opening a served app is proven against a live MCP server in ArtCraftStudioFacts.
public sealed class StudioNeuronFacts(ArtCraftFixture fixture) : IClassFixture<ArtCraftFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => ArtCraftFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnAppThatIsNotArtCraftIsRefused(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var studio = runtime.Get<IArtCraftStudio>(runtime.Id("artcraft.studio", "artcraft"));

        await Assert.ThrowsAsync<ArgumentException>(() => studio.Open("paint", TestContext.Current.CancellationToken));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnAppWithoutAnMcpServerInThisDeploymentIsRefused(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var studio = runtime.Get<IArtCraftStudio>(runtime.Id("artcraft.studio", "artcraft"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => studio.Open("photocraft", TestContext.Current.CancellationToken));
    }
}
