using DigitalBrain.Contracts;
using System.ComponentModel;
using IntoChat.OS;
using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Agents.Facts;

public enum FactKind { Profile, Fact, Insight }

public enum FactScope { Private, Circle }

public sealed record Fact(Guid Id, FactKind Kind, string Text,
    string Subject, double Confidence, int Importance,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, string? InvalidReason,
    DateTimeOffset Recorded, string SourcePublisher, long SourceSequence,
    string PromptVersion, FactScope Scope, Guid[] Links, bool Hidden);

// DedupKey: a content hash (Memory hashes the item's normalised kind, subject, text and episode ids), so Assert is
// idempotent: a known key returns its fact, current or not.
public sealed record FactAssertion(FactKind Kind, string Text, string Subject,
    double Confidence, int Importance, DateTimeOffset ValidFrom,
    string SourcePublisher, long SourceSequence, string PromptVersion,
    Guid[] Links, string DedupKey);

public sealed record FactsSnapshot(long Revision, Fact[] Facts);

[NeuronAlias("facts"), DefaultNeuronType("facts")]
public interface IFacts
{
    [Description("Record a durable fact about the user. Returns the existing fact when the dedup key was seen before.")]
    Task<Guid> Assert(FactAssertion assertion);

    [Description("End a fact's validity with a reason; the fact stays in history.")]
    Task Invalidate(Guid id, string reason, Guid? supersededBy, long expectedRevision);

    [Description("Hide a fact from recall and rendering (\"forget this\"); it stays in the journal.")]
    Task Forget(Guid id, string reason, long expectedRevision);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<FactsSnapshot> Read();

    // ValidTo == null && !Hidden
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<Fact[]> Current();

    static string Key(string brainId) => $"{brainId}/memory/facts";

    const int Cap = 512;
    const int TextLimit = 2_000;
    const int SubjectLimit = 200;
}
