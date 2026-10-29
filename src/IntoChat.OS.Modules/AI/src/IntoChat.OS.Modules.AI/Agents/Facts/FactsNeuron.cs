using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.AI.Agents.Facts.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.Agents.Facts;

[GenerateSerializer, Alias("facts.dedup")]
public sealed record FactDedup([property: Id(0)] string Key, [property: Id(1)] Guid FactId);

[ExportableState]
[GenerateSerializer, Alias("facts.state")]
public sealed record FactsState
{
    [Id(0)] public long Revision { get; init; }
    [Id(1)] public Fact[] Facts { get; init; } = [];
    [Id(2)] public FactDedup[] Dedup { get; init; } = [];
}

[Neuron("facts", State = "facts")]
internal sealed class FactsNeuron : AiNeuron<FactsState>, IFacts
{
    public async Task<Guid> Assert(FactAssertion assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        if (string.IsNullOrWhiteSpace(assertion.DedupKey)) { throw new ArgumentException("A fact needs a dedup key.", nameof(assertion)); }
        if (string.IsNullOrWhiteSpace(assertion.Text) || assertion.Text.Length > IFacts.TextLimit) { throw new ArgumentException($"A fact needs text of at most {IFacts.TextLimit} characters.", nameof(assertion)); }
        if (assertion.Subject is null || assertion.Subject.Length > IFacts.SubjectLimit) { throw new ArgumentException($"A fact needs a subject of at most {IFacts.SubjectLimit} characters.", nameof(assertion)); }
        if (assertion.Importance < 1 || assertion.Importance > 10) { throw new ArgumentOutOfRangeException(nameof(assertion), assertion.Importance, "Importance must be between 1 and 10."); }
        if (!double.IsFinite(assertion.Confidence) || assertion.Confidence < 0 || assertion.Confidence > 1) { throw new ArgumentOutOfRangeException(nameof(assertion), assertion.Confidence, "Confidence must be between 0 and 1."); }
        var known = Array.Find(Snapshot.Dedup, item => item.Key == assertion.DedupKey);
        if (known is not null) { return known.FactId; }
        if (Snapshot.Facts.Count(IsCurrent) >= IFacts.Cap) { throw new InvalidOperationException($"At most {IFacts.Cap} facts can be current; invalidate or forget one first."); }
        var id = Guid.NewGuid();
        var fact = new Fact(id, assertion.Kind, assertion.Text, assertion.Subject, assertion.Confidence, assertion.Importance,
            assertion.ValidFrom, null, null, DateTimeOffset.UtcNow, assertion.SourcePublisher ?? "", assertion.SourceSequence,
            assertion.PromptVersion ?? "", FactScope.Private, [.. assertion.Links ?? []], false);
        await Save(Snapshot with
        {
            Revision = Snapshot.Revision + 1,
            Facts = [.. Snapshot.Facts, fact],
            Dedup = [.. Snapshot.Dedup, new FactDedup(assertion.DedupKey, id)],
        }, new FactAsserted(FullKey, id, assertion.DedupKey));
        return id;
    }

    public async Task Invalidate(Guid id, string reason, Guid? supersededBy, long expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(reason)) { throw new ArgumentException("A reason is needed to invalidate a fact.", nameof(reason)); }
        var fact = Find(id, expectedRevision);
        if (fact.ValidTo is not null) { throw new InvalidOperationException("The fact is already invalid."); }
        Fact? successor = null;
        if (supersededBy is { } by)
        {
            if (by == id) { throw new ArgumentException("A fact cannot supersede itself.", nameof(supersededBy)); }
            successor = Array.Find(Snapshot.Facts, item => item.Id == by) ?? throw new ArgumentException("The superseding fact does not exist.", nameof(supersededBy));
        }
        var ended = fact with
        {
            ValidTo = DateTimeOffset.UtcNow,
            InvalidReason = reason,
            Links = successor is null ? fact.Links : [.. fact.Links.Union([successor.Id])],
        };
        var facts = Snapshot.Facts.Select(item =>
            item.Id == id ? ended
            : successor is not null && item.Id == successor.Id ? item with { Links = [.. item.Links.Union([id])] }
            : item).ToArray();
        await Save(Snapshot with { Revision = Snapshot.Revision + 1, Facts = facts }, new FactInvalidated(FullKey, id, supersededBy));
    }

    public async Task Forget(Guid id, string reason, long expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(reason)) { throw new ArgumentException("A reason is needed to forget a fact.", nameof(reason)); }
        var fact = Find(id, expectedRevision);
        await Save(Snapshot with
        {
            Revision = Snapshot.Revision + 1,
            Facts = [.. Snapshot.Facts.Select(item => item.Id == fact.Id ? item with { Hidden = true } : item)],
        }, new FactForgotten(FullKey, id));
    }

    private Fact Find(Guid id, long expectedRevision)
    {
        if (expectedRevision != Snapshot.Revision)
        { throw new RevisionConflictException($"Expected revision {expectedRevision}; current revision is {Snapshot.Revision}.", Snapshot.Revision); }
        return Array.Find(Snapshot.Facts, item => item.Id == id) ?? throw new ArgumentException("The fact does not exist.", nameof(id));
    }

    private static bool IsCurrent(Fact fact) => fact.ValidTo is null && !fact.Hidden;

    public Task<FactsSnapshot> Read() => Task.FromResult(new FactsSnapshot(Snapshot.Revision, Snapshot.Facts.ToArray()));

    public Task<Fact[]> Current() => Task.FromResult(Snapshot.Facts.Where(IsCurrent).ToArray());
}
