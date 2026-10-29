using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Progress.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Progress;

[Neuron(UIVocabulary.ProgressType)]
internal sealed class ProgressNeuron
    : UiNeuron<ProgressState>, IProgress
{
    public Task Set(bool determinate, double value, string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Determinate = determinate;
        next.Value = Math.Clamp(value, 0, 1);
        next.Label = label;
        return Save(next, new ProgressChanged(FullKey, next.Value));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ProgressState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
