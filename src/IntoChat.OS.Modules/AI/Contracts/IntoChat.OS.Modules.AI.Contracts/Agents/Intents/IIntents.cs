using DigitalBrain;
using DigitalBrain.Contracts;
using System.ComponentModel;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Agents.Intents;

public enum IntentState { Heard, Rephrased, Confirmed, Dispatched, Waiting, Done, Abandoned }

public sealed record IntentDraft(string RequestId, string Heard,
    string Conversation, Guid? Continues = null);

public sealed record IntentBrief(string Goal, string[] Inputs,
    string[] Constraints, string[] Acceptance);

// Kind is "signal" or "time".
public sealed record Cue(string Kind, string PublisherKey,
    string SignalAlias, string? Field, string? EqualTo,
    DateTimeOffset? At);

public sealed record IntentTransition(IntentState State, DateTimeOffset At,
    string RequestId, string Reason);

public sealed record Intent(Guid Id, string Heard, string Conversation,
    string RequestId, IntentBrief? Brief, double Confidence,
    IntentState State, Cue[] Cues, string[] Links,
    IntentTransition[] History);

public sealed record IntentsSnapshot(long Revision, Intent[] Intents);

[NeuronAlias("intents"), DefaultNeuronType("intents")]
public interface IIntents
{
    [Description("Record what the user asked as an intent. Returns the existing intent when the request id was heard before.")]
    Task<Guid> Declare(IntentDraft draft);

    Task Rephrase(Guid id, IntentBrief brief, double confidence, long expectedRevision);

    [Description("Move an intent to a new state: Confirmed, Dispatched, Waiting, Done or Abandoned.")]
    Task Transition(Guid id, IntentState to, string reason, long expectedRevision);

    Task Arm(Guid id, Cue[] cues, long expectedRevision);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<IntentsSnapshot> Read();

    static string Key(string brainId) => $"{brainId}/memory/intents";

    const int ActiveCap = 256;
    const int RequestIdLimit = 200;
    const int HeardLimit = 8_000;
}
