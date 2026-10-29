using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Rating;

[NeuronAlias("rating"), DefaultNeuronType(UIVocabulary.RatingType)]
public interface IRating
{
    Task Set(int max, int value);
    [DigitalBrain.ReadOnlyCall] Task<RatingState> Read();
}

[ExportableState]
public sealed class RatingState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public int Max { get; set; } = 5;
    public int Value { get; set; }
}
