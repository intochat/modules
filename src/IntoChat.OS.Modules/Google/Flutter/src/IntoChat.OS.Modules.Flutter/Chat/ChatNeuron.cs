using System.Security.Cryptography;
using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Flutter.Chat;

// A selection-bound chat follows its agent through a synapse (06-05b): the agent's change signals start a reconciliation that
// reads the agent's snapshot again. A state write that changes nothing visible (a repeated selection's receipt) saves with
// ChatReceiptRecorded, because a plain neuron saves with a signal (D-02).
[DigitalBrain.Neuron(UIVocabulary.ChatType)]
internal sealed partial class ChatNeuron : UiNeuron<ChatState>, IChat, IChatReconciliation, IChatMigration
{
    public Task Project(ChatProjection projection)
    {
        if (Snapshot.Selection is not null) { throw new InvalidOperationException("This chat follows an agent; manual projection is not allowed."); }
        return ProjectCore(projection);
    }

    private async Task ProjectCore(ChatProjection projection, ChatRunSnapshot? run = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (string.IsNullOrWhiteSpace(projection.Source) || projection.Source.Length > 512 || projection.Generation < 0 || projection.Version < 0)
        { throw new ArgumentException("A chat projection needs a source and non-negative generation and version."); }
        ArgumentNullException.ThrowIfNull(projection.Messages);
        if (projection.Messages.Length > 512 || projection.Messages.Any(message => message is null || string.IsNullOrWhiteSpace(message.Id)
            || message.Id.Length > 256 || !Enum.IsDefined(message.Role) || message.Text is null
            || message.Status is not ("running" or "completed" or "failed" or "cancelled" or "interrupted")
            || message.Activities is null || message.Activities.Any(activity => activity is null || string.IsNullOrWhiteSpace(activity.Id)
                || string.IsNullOrWhiteSpace(activity.Title) || activity.Input is null)
            || message.Activities.Select(activity => activity.Id).Distinct(StringComparer.Ordinal).Count() != message.Activities.Length)
            || projection.Messages.Select(message => message.Id).Distinct(StringComparer.Ordinal).Count() != projection.Messages.Length)
        { throw new ArgumentException("Chat messages and activities need unique stable IDs and valid content."); }
        ArgumentNullException.ThrowIfNull(projection.Actions);
        if (projection.Actions.Length > 16 || projection.Actions.Any(action => action is null
            || string.IsNullOrWhiteSpace(action.Id) || action.Id.Length > 256
            || string.IsNullOrWhiteSpace(action.Label) || action.Label.Length > 256
            || action.Payload is null || action.Payload.Length > 32_000)
            || projection.Actions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != projection.Actions.Length)
        { throw new ArgumentException("Chat actions need unique stable IDs, labels, and bounded payloads."); }

        if (Snapshot.Projection is { } current)
        {
            if (projection.Generation < current.Generation) { return; }
            if (projection.Generation == current.Generation)
            {
                if (projection.Source != current.Source) { throw new InvalidOperationException("A new chat source needs a new generation."); }
                if (projection.Version < current.Version || projection.Version == current.Version && Snapshot.Run == run) { return; }
            }
        }
        var previous = Snapshot;
        var activityChanged = previous.Run?.Source != run?.Source || previous.Run?.SelectionRevision != run?.SelectionRevision
            || previous.Run?.RunId != run?.RunId || previous.Run?.Status != run?.Status;
        var next = previous with
        {
            Name = FullKey,
            Revision = checked(previous.Revision + 1),
            Run = run,
            ActivityRevision = activityChanged ? checked(previous.ActivityRevision + 1) : previous.ActivityRevision,
            Projection = projection with
            {
                Messages = [.. projection.Messages.Select(message => message with { Activities = [.. message.Activities] })],
                Actions = [.. projection.Actions],
            },
        };
        await Save(next, new ChatChanged(FullKey, next.Revision),
            activityChanged ? new ChatRunChanged(FullKey, next.ActivityRevision, run?.Status) : null);
    }

    public async Task Activate(string source, long generation, string actionId)
    {
        var projection = Snapshot.Projection;
        if (projection is null || projection.Source != source || projection.Generation != generation
            || (projection.Actions ?? []).FirstOrDefault(action => action.Id == actionId) is not { Enabled: true } action)
        { throw new InvalidOperationException("This chat action is no longer available."); }
        // Use the canonical payload. Streaming versions do not invalidate the same action identity.
        var activated = new ChatActionActivated(FullKey, source, generation,
            action.Id, action.Payload, Guid.NewGuid());
        await Publish(activated);
        await Dispatch(UiEvent.Of(activated));
    }

    public async Task MarkRead(string source, long generation, long version, string[] messageIds)
    {
        var caller = CallerContextStamper.Require();
        if (caller.Kind != CallerKind.User || CallerContextStamper.IsModelOriginated(caller))
        { throw new UnauthorizedAccessException("Only a person can acknowledge an answer."); }
        var projection = Snapshot.Projection;
        if (projection is null || projection.Source != source || projection.Generation != generation || projection.Version != version)
        { throw new InvalidOperationException("The transcript changed. Read it again before acknowledging answers."); }
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Length > 512 || messageIds.Any(id => !projection.Messages.Any(message =>
            message.Id == id && message.Role == ChatRole.Assistant && message.Status == "completed")))
        { throw new ArgumentException("Only completed answers in this transcript can be acknowledged."); }
        var receipts = messageIds.Distinct(StringComparer.Ordinal).Select(id => new ChatReadReceipt(
            caller.PrincipalId, source, id, ContentHash(projection.Messages.Single(message => message.Id == id)))).ToArray();
        if (receipts.All(receipt => Snapshot.ReadReceipts.Contains(receipt))) { return; }
        var previous = Snapshot;
        var next = previous with
        {
            Revision = checked(previous.Revision + 1),
            ReadReceipts = [.. previous.ReadReceipts.Where(receipt => !receipts.Any(next =>
                next.PrincipalId == receipt.PrincipalId && next.Source == receipt.Source && next.MessageId == receipt.MessageId)), .. receipts],
        };
        await Save(next, new ChatChanged(FullKey, next.Revision));
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<ChatState> Read()
    {
        var principal = CallerContextStamper.TryGet(out var caller) ? caller.PrincipalId : null;
        var projection = Snapshot.Projection;
        var unread = projection is null || principal is null ? [] : projection.Messages
            .Where(message => message.Role == ChatRole.Assistant && message.Status == "completed"
                && !Snapshot.ReadReceipts.Contains(new(principal, projection.Source, message.Id, ContentHash(message))))
            .Select(message => message.Id).ToArray();
        return Task.FromResult(Snapshot with { ReadReceipts = [], SelectionReceipts = [], UnreadMessageIds = unread });
    }

    private static string ContentHash(ChatEntry message) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(message)));
}
