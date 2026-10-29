using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Toggle.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Toggle;

[Neuron(UIVocabulary.ToggleType)]
internal sealed class ToggleNeuron
    : UiNeuron<ToggleState>, IToggle
{
    public Task Set(string label, bool on)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Label = label.Trim();
        next.On = on;
        return Save(next, new ToggleChanged(FullKey, next.On));
    }

    public Task Flip()
    {
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.On = !next.On;
        return Save(next, new ToggleChanged(FullKey, next.On));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ToggleState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
