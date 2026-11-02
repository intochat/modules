using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Programs;

// A dedicated program grain for a single app ({brain}/app/{name}).
// Holds compilation state, compiled assembly SHA, settings, execution status, and subscriber cursors.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.app-program"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.app-program")]
[DigitalBrain.Access.Internal]
public interface IAppProgram
{
    [DigitalBrain.Interleaves] Task<ProgramView> Assign(Dictionary<string, string> sources, Dictionary<string, string> settings);
    [DigitalBrain.Interleaves] Task<ProgramView> AssignPrebuilt(Dictionary<string, PrebuiltBehavior> behaviors, Dictionary<string, string> settings);
    Task Retire();
    [DigitalBrain.Interleaves] Task<ProgramView?> Describe();
    Task Acknowledge(long watermark, long cpuMicroseconds, long wallMicroseconds);
    [DigitalBrain.Interleaves] Task<string> Logs(int tail);
}
