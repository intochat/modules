using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Clock.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Clock;

[Neuron(UIVocabulary.ClockType)]
internal sealed class ClockNeuron
    : UiNeuron<ClockState>, IClock
{
    public Task Set(string label, DateTimeOffset? dueAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Label = label.Trim();
        next.DueAt = dueAt;
        return Save(next, new ClockChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ClockState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
