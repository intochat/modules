
namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// One per brain: the behaviors it wants running, their durable subscriptions, and the one sandbox
// session that runs them all.
// A plain neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runtime"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runtime")]
public interface IBrainRuntime
{
    // A pure read: it never waits for a turn in progress.
    [DigitalBrain.Interleaves] Task<BrainRuntimeSnapshot> Inspect(CancellationToken cancellationToken = default);
    // Sign-in and the shell call this: ensures a session when anything is assigned, otherwise nothing.
    Task Touch(CancellationToken cancellationToken = default);
}
