using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Slider;

[NeuronAlias("slider"), DefaultNeuronType(UIVocabulary.SliderType)]
public interface ISlider
{
    Task Configure(double min, double max, double step);
    Task SetValue(double value);
    [DigitalBrain.ReadOnlyCall] Task<SliderState> Read();
}

[ExportableState]
public sealed class SliderState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public double Min { get; set; }
    public double Max { get; set; } = 1;
    public double Step { get; set; } = 1;
    public double Value { get; set; }
}
