using DigitalBrain;
using IntoChat.OS.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Flutter.Chat;

// A repeated selection only adds its operation receipt: a state change nothing reads as a chat change.
[DigitalBrain.Signal("ui.chat-receipt-recorded")]
internal sealed record ChatReceiptRecorded(string Name) : DigitalBrain.Signal;

internal sealed partial class ChatNeuron
{
    // The source this activation holds its synapse on; a selection change removes the previous one (06-05b: the synapse is
    // durable and delivers what was published while the chat was away, so no watermark or renewal is kept).
    private DigitalBrain.NeuronId? _following;
    private bool _dirty;
    private bool _queued;
    private IChatAgentSource Adapter => ServiceProvider.GetRequiredService<IChatAgentSource>();

    public async Task<ChatSelection> SelectAgent(string agent, long expectedSelectionRevision, Guid operationId)
    {
        ValidateSelection(agent, operationId);
        if (Snapshot.SelectionReceipts.FirstOrDefault(receipt => receipt.OperationId == operationId) is { } receipt)
        {
            if (receipt.Selection.Agent != agent) { throw new InvalidOperationException("This operation selected a different agent."); }
            await Recover();
            return receipt.Selection;
        }
        var revision = Snapshot.Selection?.Revision ?? 0;
        if (revision != expectedSelectionRevision) { throw new RevisionConflictException("The selected conversation changed.", revision); }
        var selected = Snapshot.Selection?.Agent == agent ? Snapshot.Selection : new ChatSelection(agent, checked(revision + 1));
        await CommitSelection(selected, operationId);
        await Recover();
        return selected;
    }

    public async Task AdoptAgent(string agent, long generation, Guid operationId)
    {
        ValidateSelection(agent, operationId);
        if (Snapshot.SelectionReceipts.FirstOrDefault(receipt => receipt.OperationId == operationId) is { } receipt)
        {
            if (receipt.Selection.Agent != agent || receipt.Selection.Revision != generation)
            { throw new InvalidOperationException("This migration operation adopted a different selection."); }
            await Recover();
            return;
        }
        if (Snapshot.Selection is { } selected)
        {
            if (selected.Agent != agent || selected.Revision != generation) { throw new InvalidOperationException("This chat has already been bound to a different selection."); }
            await CommitSelection(selected, operationId);
            await Recover();
            return;
        }
        if (generation < 0 || generation < (Snapshot.Projection?.Generation ?? 0)
            || (Snapshot.Projection is { } projection && projection.Source != agent && generation <= projection.Generation))
        { throw new InvalidOperationException("Migration must retain the current chat source and generation."); }
        await CommitSelection(new(agent, generation), operationId);
        await Recover();
    }

    private void ValidateSelection(string agent, Guid operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agent);
        if (agent.Length > 512 || operationId == Guid.Empty) { throw new ArgumentException("Selection needs a bounded source and an operation ID."); }
        if (!BrainKey.BelongsTo(agent, Id.Brain.Value))
        { throw new UnauthorizedAccessException("A chat can only select an agent in its own brain."); }
        _ = Adapter;
    }

    private async Task CommitSelection(ChatSelection selected, Guid operationId)
    {
        // Authorize/resolve the source before persisting a binding. This also rejects unavailable adapters.
        await Adapter.Read(selected.Agent, selected.Revision);
        var previous = Snapshot;
        var changed = previous.Selection != selected;
        var clearRun = changed && previous.Run is { } run
            && (run.Source != selected.Agent || run.SelectionRevision != selected.Revision);
        var next = previous with
        {
            Name = FullKey,
            Selection = selected,
            SelectionReceipts = [.. previous.SelectionReceipts, new(operationId, selected)],
            // Do not expose the old conversation/actions while the new projection is being recovered.
            Projection = changed && previous.Projection is { } projection
                && (projection.Source != selected.Agent || projection.Generation != selected.Revision) ? null : previous.Projection,
            Run = clearRun ? null : previous.Run,
            ActivityRevision = clearRun ? checked(previous.ActivityRevision + 1) : previous.ActivityRevision,
            Revision = changed ? checked(previous.Revision + 1) : previous.Revision,
        };
        if (changed)
        {
            var selectionSignal = new ChatSelectionChanged(FullKey, selected);
            var changedSignal = new ChatChanged(FullKey, next.Revision);
            await Save(next, clearRun
                ? [selectionSignal, changedSignal, new ChatRunChanged(FullKey, next.ActivityRevision)]
                : [selectionSignal, changedSignal]);
            // The previous agent no longer feeds this chat.
            if (previous.Selection is { } left && left.Agent != selected.Agent) { await Unfollow(Adapter.Subscription(left.Agent).Source); }
        }
        else { await Save(next, new ChatReceiptRecorded(FullKey)); }
    }

    private async Task Unfollow(DigitalBrain.NeuronId source)
    {
        await Context.Synapses.Remove(source);
        if (_following == source) { _following = null; }
    }

    internal async Task Recover()
    {
        if (Snapshot.Selection is not { } selected) { return; }
        var subscription = Adapter.Subscription(selected.Agent);
        if (_following is { } old && old != subscription.Source) { await Unfollow(old); }
        // Idempotent: the source admits the synapse again on every Ensure.
        await Context.Synapses.Ensure(subscription.Source, subscription.Signals);
        _following = subscription.Source;
        _dirty = true;
        await Reconcile();
    }

    // Any change signal of the followed agent: the snapshot is read again, one reconciliation at a time.
    [DigitalBrain.On]
    public Task On(DigitalBrain.Signal changed)
    {
        _ = changed;
        _dirty = true;
        return QueueReconciliation();
    }

    private Task QueueReconciliation()
    {
        if (!_dirty || _queued) { return Task.CompletedTask; }
        _queued = true;
        return Context.Get<IChatReconciliation>(Id).Reconcile();
    }

    public async Task Reconcile()
    {
        _queued = false;
        if (_following is null && Snapshot.Selection is not null) { await Recover(); return; }
        if (!_dirty || Snapshot.Selection is not { } selected) { return; }
        _dirty = false;
        try
        {
            var snapshot = await Adapter.Read(selected.Agent, selected.Revision);
            if (Snapshot.Selection != selected) { return; }
            await ProjectCore(snapshot.Projection, snapshot.Run);
        }
        catch
        {
            // A failed snapshot stays dirty; the next change signal or activation reads it again.
            _dirty = true;
            throw;
        }
    }

    protected override Task OnActivate(CancellationToken cancellationToken)
    {
        if (Snapshot.Selection is null) { return Task.CompletedTask; }
        _dirty = true;
        return QueueReconciliation();
    }
}
