using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Clock;

[NeuronAlias("clock"), DefaultNeuronType(UIVocabulary.ClockType)]
public interface IClock
{
    Task Set(string label, DateTimeOffset? dueAt);
    [DigitalBrain.ReadOnlyCall] Task<ClockState> Read();
}

[ExportableState]
public sealed class ClockState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Label { get; set; } = "";
    public DateTimeOffset? DueAt { get; set; }
}
