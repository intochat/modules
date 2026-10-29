using System.ComponentModel;
using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Memory;

public sealed record MemoryEpisode(
    string Id,
    string Agent,
    string Summary,
    int Importance,
    DateTimeOffset RecordedAt);

public sealed record MemoryFact(
    string Id,
    string Subject,
    string Text,
    int Importance,
    DateTimeOffset RecordedAt);

public sealed record MemoryRecallHit(
    string Id,
    string Kind,
    string Text,
    double Score);

public sealed record MemorySnapshot(
    long Revision,
    MemoryEpisode[] Episodes,
    MemoryFact[] Facts);

[DigitalBrain.Signal("ai.memory.changed")]
public sealed record MemoryChanged(
    long Revision,
    int EpisodeCount,
    int FactCount) : DigitalBrain.Signal;

[NeuronAlias("ai.memory"), DefaultNeuronType("ai.memory")]
[Description("In-process durable memory of episodes and facts for a brain, with zero external database dependency.")]
public interface IMemory
{
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<MemorySnapshot> Read();

    [Description("Record a summarized conversation episode.")]
    Task<MemoryEpisode> CaptureEpisode(string agent, string summary, int importance = 5);

    [Description("Record a durable fact about the user or brain.")]
    Task<MemoryFact> RememberFact(string subject, string text, int importance = 5);

    [Description("Recall the most relevant facts and episodes matching the query.")]
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<MemoryRecallHit[]> Recall(string query, int limit = 5);
}
