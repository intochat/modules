using IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// Recent is the latest 200 entries after excluding a whole owner, ordered oldest first.
public sealed record BrainRunsSnapshot(
    long Revision,
    BrainRunEntry[] Recent,
    BrainRunStatistics[] Statistics);

public sealed record BrainRunEntry(
    long Sequence,
    string Owner,
    string Behavior,
    RunOutcome Outcome,
    long DurationMicroseconds,
    string? TraceId,
    string Mark);

public sealed record BrainRunSample(
    string Mark,
    long Microseconds);

public sealed record BrainRunStatistics(
    string Behavior,
    string Hash,
    BrainRunSample[] Samples)
{
    public long P50Microseconds => Samples.Length == 0 ? 0
        : Samples.Select(sample => sample.Microseconds).Order().ElementAt((Samples.Length - 1) / 2);
}
