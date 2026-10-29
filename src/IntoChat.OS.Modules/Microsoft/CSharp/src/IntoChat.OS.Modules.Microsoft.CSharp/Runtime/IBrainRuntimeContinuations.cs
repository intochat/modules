
namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// Same grain as the brain runtime. Callers reach it through IWorkContinuation, not the script edge.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runtime-continuations"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runtime")]
internal interface IBrainRuntimeContinuations
{
    Task Acquire(string token);

    Task Release(string token);
}
