using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// Rules are judged in the brain's session whether or not it has programs: this ensures it and returns once the host holds it.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runtime-session"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runtime")]
internal interface IBrainRuntimeSession
{
    Task Hold(CancellationToken cancellationToken = default);
}
