using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Color;

[NeuronAlias("color"), DefaultNeuronType(UIVocabulary.ColorType)]
public interface IColor
{
    Task Set(string hex);
    [DigitalBrain.ReadOnlyCall] Task<ColorState> Read();
}

[ExportableState]
public sealed class ColorState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Hex { get; set; } = "#000000";
}
