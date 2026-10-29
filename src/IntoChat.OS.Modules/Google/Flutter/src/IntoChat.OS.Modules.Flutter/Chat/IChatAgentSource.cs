namespace IntoChat.OS.Modules.Flutter.Chat;

// Composition seam: the UI runtime need not reference AI implementation/contracts.
// Implementations resolve one authoritative agent and translate its recoverable snapshot.
public interface IChatAgentSource
{
    // The neuron a chat following key holds a synapse on, and the [Signal] types that say it changed (06-05b: a chat is a plain
    // neuron; any of them makes it read the snapshot again).
    ChatAgentSubscription Subscription(string key);
    Task<ChatAgentSnapshot> Read(string key, long selectionRevision);
}

public sealed record ChatAgentSubscription(DigitalBrain.NeuronId Source, Type[] Signals);

public sealed record ChatAgentSnapshot(ChatProjection Projection, ChatRunSnapshot? Run);

// Infrastructure only: neither interface is exposed to scripts/model tools.
[DigitalBrain.Contracts.NeuronAlias("ui.chat-migration"), DigitalBrain.Contracts.DefaultNeuronType(UIVocabulary.ChatType)]
public interface IChatMigration
{
    Task AdoptAgent(string agent, long generation, Guid operationId);
}

// The chat's reconciliation as its own one-way turn: a deferred turn would hold the call that queued it.
[DigitalBrain.Contracts.NeuronAlias("ui.chat-reconciliation"), DigitalBrain.Contracts.DefaultNeuronType(UIVocabulary.ChatType)]
internal interface IChatReconciliation
{
    [DigitalBrain.OneWayCall] Task Reconcile();
}
