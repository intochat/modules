using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Slider.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Slider;

[Neuron(UIVocabulary.SliderType)]
internal sealed class SliderNeuron
    : UiNeuron<SliderState>, ISlider
{
    public Task Configure(double min, double max, double step)
    {
        if (max <= min) { throw new ArgumentOutOfRangeException(nameof(max)); }
        if (step <= 0) { throw new ArgumentOutOfRangeException(nameof(step)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Min = min;
        next.Max = max;
        next.Step = step;
        next.Value = Math.Clamp(next.Value, min, max);
        return Save(next, new SliderChanged(FullKey, next.Value));
    }

    public Task SetValue(double value)
    {
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Value = Math.Clamp(value, next.Min, next.Max);
        return Save(next, new SliderChanged(FullKey, next.Value));
    }

    [DigitalBrain.ReadOnlyCall] public Task<SliderState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
