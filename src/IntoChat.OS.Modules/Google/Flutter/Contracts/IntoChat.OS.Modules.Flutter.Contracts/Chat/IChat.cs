using DigitalBrain.Contracts;
using DigitalBrain;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Chat;

// A recoverable UI projection. The source neuron owns the conversation and runs its turns.
[NeuronAlias("chat"), DefaultNeuronType(UIVocabulary.ChatType)]
public interface IChat
{
    Task<ChatSelection> SelectAgent(string agent, long expectedSelectionRevision, Guid operationId);
    Task Project(ChatProjection projection);
    // IDs identify immutable intents within a generation; use a new ID when the action target changes.
    Task Activate(string source, long generation, string actionId);
    Task MarkRead(string source, long generation, long version, string[] messageIds);
    [DigitalBrain.ReadOnlyCall] Task<ChatState> Read();
}

public sealed record ChatProjection(
    string Source,
    long Generation,
    long Version,
    ChatEntry[] Messages)
{
    public ChatAction[] Actions { get; init; } = [];
}

public sealed record ChatAction(
    string Id,
    string Label,
    string Payload,
    bool Enabled = true);

[Signal("ui.chat-action-activated")]
public sealed record ChatActionActivated(
    string Name,
    string Source,
    long Generation,
    string ActionId,
    string Payload,
    Guid RequestId) : DigitalBrain.Signal;

[ExportableState]
public sealed record ChatState
{
    public string Name { get; init; } = "";
    public long Revision { get; init; }
    public ChatProjection? Projection { get; init; }
    public ChatReadReceipt[] ReadReceipts { get; init; } = [];
    public string[] UnreadMessageIds { get; init; } = [];
    public ChatSelection? Selection { get; init; }
    public ChatRunSnapshot? Run { get; init; }
    public long ActivityRevision { get; init; }
    // Receipts are persisted with selection, but never exposed by Read.
    public ChatSelectionReceipt[] SelectionReceipts { get; init; } = [];
}

public sealed record ChatSelection(string Agent, long Revision);

public sealed record ChatSelectionReceipt(Guid OperationId, ChatSelection Selection);

public enum ChatRunStatus { Running, Completed, Failed, Cancelled, Interrupted }

public sealed record ChatRunSnapshot(
    string Source, long SelectionRevision,
    long Version, string RunId, ChatRunStatus Status);

[Signal("ui.chat-selection-changed")]
public sealed record ChatSelectionChanged(string Name, ChatSelection Selection) : DigitalBrain.Signal;

[Signal("ui.chat-run-changed")]
public sealed record ChatRunChanged(
    string Name,
    long ActivityRevision,
    ChatRunStatus? Status = null) : DigitalBrain.Signal;

public sealed record ChatReadReceipt(
    string PrincipalId,
    string Source,
    string MessageId,
    string ContentHash);

[Signal("ui.chat-changed")]
public sealed record ChatChanged(string Name, long Revision) : DigitalBrain.Signal;
