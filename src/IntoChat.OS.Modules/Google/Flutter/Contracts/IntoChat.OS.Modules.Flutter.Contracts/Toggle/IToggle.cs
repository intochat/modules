using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Toggle;

[NeuronAlias("toggle"), DefaultNeuronType(UIVocabulary.ToggleType)]
public interface IToggle
{
    Task Set(string label, bool on);
    Task Flip();
    [DigitalBrain.ReadOnlyCall] Task<ToggleState> Read();
}

[ExportableState]
public sealed class ToggleState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Label { get; set; } = "";
    public bool On { get; set; }
}
