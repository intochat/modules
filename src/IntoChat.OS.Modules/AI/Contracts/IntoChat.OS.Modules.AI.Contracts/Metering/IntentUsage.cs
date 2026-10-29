using DigitalBrain.Contracts;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Metering;

public enum MeterKind { Chat, Embedding }

// One provider call's reported usage. Every token class is nullable: null means the provider did not
// report that class, never zero. UsageReported is false when the provider returned no usage at all, so
// an unreported call is distinguishable from a free one.
public sealed record TokenUsageEntry(
    MeterKind Meter,
    string Provider,
    string Model,
    long? InputTokens,
    long? CachedInputTokens,
    long? ReasoningTokens,
    long? OutputTokens,
    long? TotalTokens,
    bool UsageReported,
    DateTimeOffset At);

public sealed record IntentTokenUsage(
    string IntentId,
    TokenUsageEntry[] Entries);

// The durable per-intent usage row. Keyed by intent id and owned by the AI module; P1.4 receipts and
// the P2.2 Compute meter consume it rather than deriving usage from sampled traces.
[NeuronAlias("ai.intent-usage")]
[DefaultNeuronType("ai-intent-usage")]
public interface IIntentUsage
{
    Task RecordAsync(TokenUsageEntry entry, CancellationToken cancellationToken = default);
    Task RecordBatchAsync(TokenUsageEntry[] entries, CancellationToken cancellationToken = default);
    Task<IntentTokenUsage> ReadAsync(CancellationToken cancellationToken = default);
}

// Entries were recorded for an intent (06-05: a plain neuron's state changes with a signal).
[DigitalBrain.Signal("ai.intent-usage-recorded")]
public sealed record IntentUsageRecorded(string IntentId, int Count) : DigitalBrain.Signal;
