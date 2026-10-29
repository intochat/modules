using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Progress;

[NeuronAlias("progress"), DefaultNeuronType(UIVocabulary.ProgressType)]
public interface IProgress
{
    Task Set(bool determinate, double value, string label);
    [DigitalBrain.ReadOnlyCall] Task<ProgressState> Read();
}

[ExportableState]
public sealed class ProgressState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public bool Determinate { get; set; }
    public double Value { get; set; }
    public string Label { get; set; } = "";
}
