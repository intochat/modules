using DigitalBrain.Access;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

[DigitalBrain.Contracts.NeuronAlias("tests.csharp.program-restorer"), DigitalBrain.Contracts.DefaultNeuronType("tests.csharp.program-restorer")]
[Public]
public interface IProgramRestorer
{
    Task Restore(string owner, Dictionary<string, string> sources);
}

[DigitalBrain.Neuron("tests.csharp.program-restorer")]
public sealed class ProgramRestorer : DigitalBrain.Neuron, IProgramRestorer
{
    public async Task Restore(string owner, Dictionary<string, string> sources)
        => await Get<IBrainPrograms>(new(Id.Brain, BrainRuntimeNeuron.GrainTypeName, BrainRuntimeKeys.Name)).Assign(owner, sources, []);
}
