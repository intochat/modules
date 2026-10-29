using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.AI.Metering;

[GenerateSerializer, Alias("ai.intent-usage-storage")]
internal sealed record IntentUsageStorage
{
    [Id(0)] public List<TokenUsageEntry> Entries { get; init; } = [];
}

[Neuron("ai-intent-usage", State = "intent-usage")]
internal sealed class IntentUsageNeuron : AiNeuron<IntentUsageStorage>, IIntentUsage
{
    public Task RecordAsync(TokenUsageEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);
        return Append([entry]);
    }

    public Task RecordBatchAsync(TokenUsageEntry[] entries, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entries is null || entries.Length == 0) { return Task.CompletedTask; }
        return Append(entries);
    }

    // A plain neuron saves with the signal that says what changed (D-02): the entries recorded.
    private Task Append(TokenUsageEntry[] entries)
        => Save(new IntentUsageStorage { Entries = [.. Snapshot.Entries, .. entries] }, new IntentUsageRecorded(FullKey, entries.Length));

    public Task<IntentTokenUsage> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new IntentTokenUsage(FullKey, [.. Snapshot.Entries]));
    }
}
