
namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// How an Assign, compiled and stored while the runtime went on, takes a turn of the runtime to change its state.
// Sent by the runtime to itself: the ticket names a program only that activation compiled, so no other caller
// has anything to apply.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runtime-program-changes"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runtime")]
internal interface IBrainProgramChanges
{
    Task<ProgramView> Apply(long ticket);
}
