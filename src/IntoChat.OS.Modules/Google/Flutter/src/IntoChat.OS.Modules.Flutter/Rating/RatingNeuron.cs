using DigitalBrain;
using IntoChat.OS.Modules.Flutter.Rating.Signals;

namespace IntoChat.OS.Modules.Flutter.Rating;

[Neuron(UIVocabulary.RatingType)]
internal sealed class RatingNeuron : UiNeuron<RatingState>, IRating
{
    public Task Set(int max, int value)
    {
        if (max < 1) { throw new ArgumentOutOfRangeException(nameof(max)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Max = max;
        next.Value = Math.Clamp(value, 0, max);
        return Save(next, new RatingChanged(FullKey, next.Value));
    }

    [DigitalBrain.ReadOnlyCall] public Task<RatingState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
