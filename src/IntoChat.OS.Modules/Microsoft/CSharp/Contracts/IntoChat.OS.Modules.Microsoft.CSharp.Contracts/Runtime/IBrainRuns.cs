
namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// One per brain, keyed BrainRunsKeys.For(scope): where the brain's synapse runs become facts. It publishes
// BehaviorStarted and BehaviorRan for the brain runtime, which cannot publish them itself because no
// behavior may subscribe to it. Its durable snapshots accompany the existing public signals.
// A plain neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runs"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runs")]
public interface IBrainRuns
{
    Task<BrainRunsSnapshot> Read(string? excludedOwner = null);
}

// Snapshot consumers need one invalidation per committed revision, not every execution fact.
// Behaviors names the runs committed in that revision. A subscriber does not wake for a revision
// that only records itself: its own completion is not a new fact about the other runs it displays.
[DigitalBrain.Signal("microsoft.csharp.runs-changed")]
public sealed record BrainRunsChanged(long Revision, string[]? Behaviors = null) : DigitalBrain.Signal;
