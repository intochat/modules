
namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// One-way so the publishing neuron never waits on this brain's runtime.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runtime-arrivals"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runtime")]
internal interface IBrainRuntimeArrivals
{
    // Reads every source's part log from its subscriptions' watermarks; sent by a reactivated runtime to itself.
    [DigitalBrain.OneWayCall] Task CatchUp();

    // Reads one source's part log from its subscriptions' watermarks, once the catch-up watched it.
    [DigitalBrain.OneWayCall] Task Read(string source);
}
