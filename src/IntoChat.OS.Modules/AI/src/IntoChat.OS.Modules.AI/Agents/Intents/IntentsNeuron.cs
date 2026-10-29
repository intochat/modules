using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.AI.Agents.Intents.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.Agents.Intents;

[ExportableState]
[GenerateSerializer, Alias("intents.state")]
public sealed record IntentsState
{
    [Id(0)] public long Revision { get; init; }
    [Id(1)] public Intent[] Intents { get; init; } = [];
}

[Neuron("intents", State = "intents")]
internal sealed class IntentsNeuron
    : AiNeuron<IntentsState>, IIntents
{
    public async Task<Guid> Declare(IntentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(draft.RequestId) || draft.RequestId.Length > IIntents.RequestIdLimit) { throw new ArgumentException($"An intent needs a request id of at most {IIntents.RequestIdLimit} characters.", nameof(draft)); }
        if (string.IsNullOrWhiteSpace(draft.Heard) || draft.Heard.Length > IIntents.HeardLimit) { throw new ArgumentException($"An intent needs heard text of at most {IIntents.HeardLimit} characters.", nameof(draft)); }
        var known = Array.Find(Snapshot.Intents, item => item.RequestId == draft.RequestId);
        if (known is not null) { return known.Id; }
        if (draft.Continues is { } continues)
        {
            if (Array.Exists(Snapshot.Intents, item => item.Id == continues)) { return continues; }
            throw new ArgumentException("The intent to continue does not exist.", nameof(draft));
        }
        var active = Snapshot.Intents.Count(item => item.State is not (IntentState.Done or IntentState.Abandoned));
        if (active >= IIntents.ActiveCap) { throw new InvalidOperationException($"At most {IIntents.ActiveCap} intents can be active; finish or abandon one first."); }
        var id = Guid.NewGuid();
        var intent = new Intent(id, draft.Heard, draft.Conversation ?? "", draft.RequestId, null, 0, IntentState.Heard, [], [],
            [new IntentTransition(IntentState.Heard, DateTimeOffset.UtcNow, draft.RequestId, "heard")]);
        var revision = Snapshot.Revision + 1;
        await Save(Snapshot with { Revision = revision, Intents = [.. Snapshot.Intents, intent] }, new IntentHeard(FullKey, id, draft.RequestId));
        return id;
    }

    public async Task Rephrase(Guid id, IntentBrief brief, double confidence, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(brief);
        if (double.IsNaN(confidence) || confidence < 0 || confidence > 1) { throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0 and 1."); }
        var intent = Find(id, expectedRevision);
        if (intent.State is IntentState.Done or IntentState.Abandoned) { throw new InvalidOperationException($"An intent in state {intent.State} cannot be rephrased."); }
        var keeps = intent.State is IntentState.Dispatched or IntentState.Waiting;
        var next = intent with
        {
            Brief = brief,
            Confidence = confidence,
            State = keeps ? intent.State : IntentState.Rephrased,
            History = keeps ? intent.History : [.. intent.History, new IntentTransition(IntentState.Rephrased, DateTimeOffset.UtcNow, intent.RequestId, "rephrased")],
        };
        await Save(Replace(next), new IntentRephrased(FullKey, id, intent.RequestId, confidence));
    }

    public async Task Transition(Guid id, IntentState to, string reason, long expectedRevision)
    {
        var intent = Find(id, expectedRevision);
        var legal = (intent.State, to) switch
        {
            (IntentState.Rephrased, IntentState.Confirmed) => true,
            (IntentState.Confirmed, IntentState.Dispatched) => true,
            (IntentState.Dispatched, IntentState.Waiting) => true,
            (IntentState.Waiting, IntentState.Dispatched) => true,
            (IntentState.Dispatched or IntentState.Waiting, IntentState.Done) => true,
            (IntentState.Heard or IntentState.Rephrased or IntentState.Confirmed or IntentState.Dispatched or IntentState.Waiting, IntentState.Abandoned) => true,
            _ => false,
        };
        if (!legal) { throw new InvalidOperationException($"An intent cannot move from {intent.State} to {to}."); }
        var next = intent with
        {
            State = to,
            History = [.. intent.History, new IntentTransition(to, DateTimeOffset.UtcNow, intent.RequestId, reason)],
        };
        await Save(Replace(next), new IntentTransitioned(FullKey, id, intent.State, to));
    }

    public async Task Arm(Guid id, Cue[] cues, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var intent = Find(id, expectedRevision);
        if (intent.State is IntentState.Done or IntentState.Abandoned) { throw new InvalidOperationException($"An intent in state {intent.State} cannot be armed."); }
        await Save(Replace(intent with { Cues = [.. cues] }), new IntentArmed(FullKey, id));
    }

    private Intent Find(Guid id, long expectedRevision)
    {
        if (expectedRevision != Snapshot.Revision)
        { throw new RevisionConflictException($"Expected revision {expectedRevision}; current revision is {Snapshot.Revision}.", Snapshot.Revision); }
        return Array.Find(Snapshot.Intents, item => item.Id == id) ?? throw new ArgumentException("The intent does not exist.", nameof(id));
    }

    private IntentsState Replace(Intent next) =>
        Snapshot with { Revision = Snapshot.Revision + 1, Intents = [.. Snapshot.Intents.Select(item => item.Id == next.Id ? next : item)] };

    public Task<IntentsSnapshot> Read() => Task.FromResult(new IntentsSnapshot(Snapshot.Revision, Snapshot.Intents.ToArray()));
}
