using DigitalBrain.Testing;

namespace IntoChat.OS.Modules.Microsoft.Roslyn.Tests;

// The plain workspace neuron (06-06a), a cluster-scoped singleton keyed by the workspace key. The Unit tier runs each fact on
// TestOS; the Integration tier compiles this same file and runs each on TestOS and on SiloHost.Local (R4-05).
public sealed class RoslynNeuronFacts(RoslynFixture fixture)
{
    public static TheoryData<NeuronRuntimeKind> Kinds => RoslynFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AWorkspaceNothingOpenedReadsAsNotOpenedAtGenerationZero(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var roslyn = runtime.Get<IRoslyn>(runtime.Id("microsoft.roslyn", "fresh-" + kind));

        var snapshot = await roslyn.Read();

        Assert.Equal((WorkspacePhase.NotOpened, 0L, null), (snapshot.Phase, snapshot.Generation, snapshot.SolutionPath));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task ABlankSolutionPathAndAReloadOfNothingAreRefused(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var roslyn = runtime.Get<IRoslyn>(runtime.Id("microsoft.roslyn", "refusals-" + kind));

        await Assert.ThrowsAsync<ArgumentException>(() => roslyn.Open(new OpenWorkspace(" ")));
        await Assert.ThrowsAsync<InvalidOperationException>(roslyn.Reload);
    }
}
