using IntoChat.OS.Modules.Flutter.VoiceInput;
using IntoChat.OS.Framework;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Chat;

[GenerateSerializer, Alias("ui.composer-storage")]
[ExportableState]
internal sealed record ComposerStorage
{
    [Id(0)] public ComposerState State { get; init; } = new();
    [Id(1)] public ComposerAudio? Audio { get; init; }
}

// A bound composer follows its chat's selection through a synapse on ChatSelectionChanged (06-05b); the synapse is durable,
// so nothing published while the composer was away is lost and no watermark or renewal is kept.
[DigitalBrain.Neuron(UIVocabulary.ComposerType)]
internal sealed class ComposerNeuron : UiNeuron<ComposerStorage>, IComposer, IComposerReconciliation, IComposerMigration
{
    private ComposerState Current => Snapshot.State;

    public async Task Follow(string chat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chat);
        if (chat.Length > 512 || !BrainKey.BelongsTo(chat, Id.Brain.Value))
        { throw new UnauthorizedAccessException("The followed chat must belong to the composer's brain."); }
        if (Current.FollowedChat is { } existing && existing != chat)
        { throw new InvalidOperationException("This composer already follows another chat."); }
        if (Current.FollowedChat is null) { await Save(Current with { FollowedChat = chat }, Snapshot.Audio); }
        await RecoverFollowing();
    }

    protected override Task OnActivate(CancellationToken cancellationToken)
        => Current.FollowedChat is { } chat ? Context.Get<IComposerReconciliation>(Id).Reconcile(chat) : Task.CompletedTask;

    internal async Task RecoverFollowing()
    {
        if (Current.FollowedChat is not { } chat) { return; }
        await Reconcile(chat);
    }

    // The followed chat's selection changed: reconcile as its own turn, so the chat publishing it never waits on this one.
    [DigitalBrain.On]
    public Task On(ChatSelectionChanged changed)
        => changed.Name == Current.FollowedChat ? Context.Get<IComposerReconciliation>(Id).Reconcile(changed.Name) : Task.CompletedTask;

    private IChat Chat(string chat) => Context.Get<IChat>(new DigitalBrain.NeuronId(Id.Brain, UIVocabulary.ChatType, chat[(Id.Brain.Value.Length + 1)..]));

    // Idempotent: the chat admits the synapse again on every Ensure.
    private Task WatchChat(string chat)
        => Context.Synapses.Ensure<ChatSelectionChanged>(new DigitalBrain.NeuronId(Id.Brain, UIVocabulary.ChatType, chat[(Id.Brain.Value.Length + 1)..]));

    public async Task Reconcile(string chat)
    {
        if (Current.FollowedChat != chat) { return; }
        await WatchChat(chat);
        var selection = (await Chat(chat).Read()).Selection;
        if (Current.FollowedChat == chat && selection is not null) { await ApplySelection(selection); }
    }

    private Task ApplySelection(ChatSelection selection)
    {
        if (selection.Revision < Current.Generation) { return Task.CompletedTask; }
        if (selection.Revision == Current.Generation && selection.Agent == Current.Context) { return Task.CompletedTask; }
        return Save(Current with
        {
            Context = selection.Agent,
            Generation = selection.Revision,
            Draft = "",
            DraftVersion = Current.DraftVersion + 1,
            Voice = null,
            Error = null,
        }, null);
    }

    public async Task AdoptFollow(string chat)
    {
        if (Current.FollowedChat is { } followed)
        {
            if (followed != chat) { throw new InvalidOperationException("This composer follows another chat."); }
            await RecoverFollowing();
            return;
        }
        if (!BrainKey.BelongsTo(chat, Id.Brain.Value))
        { throw new UnauthorizedAccessException("The followed chat must belong to the composer's brain."); }
        await WatchChat(chat);
        var selection = (await Chat(chat).Read()).Selection ?? throw new InvalidOperationException("Select the migrated chat source first.");
        if (Current.Context.Length != 0 && (Current.Context != selection.Agent || Current.Generation > selection.Revision))
        { throw new InvalidOperationException("Migration must preserve the composer's selected source and cannot regress its generation."); }
        await Save(Current with
        {
            FollowedChat = chat,
            Context = selection.Agent,
            Generation = selection.Revision,
            Submissions = [.. Current.Submissions.Select(item => item.Generation < 0 && item.Context == Current.Context
                ? item with { Generation = selection.Revision } : item)],
        }, Snapshot.Audio);
        await RecoverFollowing();
    }

    private void RequireStandalone()
    {
        if (Current.FollowedChat is not null)
        { throw new InvalidOperationException("A bound composer requires the displayed selection revision."); }
    }

    private async Task ValidateSelection(string context, long generation)
    {
        if (Current.FollowedChat is { } chat)
        {
            var selection = (await Chat(chat).Read()).Selection;
            if (selection is null || selection.Agent != context || selection.Revision != generation)
            { throw new InvalidOperationException("The conversation changed. Review the current draft before continuing."); }
            await ApplySelection(selection);
        }
        RequireContext(context);
        if (Current.Generation != generation)
        { throw new InvalidOperationException("The conversation changed. Review the current draft before continuing."); }
    }

    public async Task Configure(string context, long generation, string label = "Message")
    {
        RequireStandalone();
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (context.Length > 512 || label.Length > 128 || generation < 0) { throw new ArgumentException("Invalid composer configuration."); }
        if (generation < Current.Generation) { return; }
        if (generation == Current.Generation && context != Current.Context) { throw new InvalidOperationException("A new context requires a new generation."); }
        if (generation == Current.Generation && label == Current.Label) { return; }
        var changed = context != Current.Context || generation != Current.Generation;
        await Save(Current with
        {
            Context = context,
            Generation = generation,
            Label = label,
            Draft = changed ? "" : Current.Draft,
            DraftVersion = changed ? Current.DraftVersion + 1 : Current.DraftVersion,
            Voice = changed ? null : Current.Voice,
            Error = changed ? null : Current.Error,
        }, changed ? null : Snapshot.Audio);
    }

    public Task Edit(string context, string value)
    {
        RequireStandalone();
        return EditDraft(context, value);
    }

    public async Task Edit(string context, long selectionRevision, string value)
    {
        await ValidateSelection(context, selectionRevision);
        await EditDraft(context, value);
    }

    private Task EditDraft(string context, string value)
    {
        RequireContext(context);
        ValidateText(value);
        if (value == Current.Draft) { return Task.CompletedTask; }
        return Save(Current with { Draft = value, DraftVersion = Current.DraftVersion + 1, Error = null }, Snapshot.Audio);
    }

    public Task<Guid?> Submit(Guid requestId, string context, string value)
    {
        RequireStandalone();
        return SubmitDraft(requestId, context, Current.Generation, value, checkReplayGeneration: false);
    }

    public Task<Guid?> Submit(Guid requestId, string context, long selectionRevision, string value)
        => SubmitDraft(requestId, context, selectionRevision, value);

    private async Task<Guid?> SubmitDraft(Guid requestId, string context, long selectionRevision, string value, bool checkReplayGeneration = true)
    {
        ValidateId(requestId);
        ValidateText(value);
        var text = value.Trim();
        if (Current.Submissions.FirstOrDefault(request => request.Id == requestId) is { } prior)
        {
            if (prior.Context != context || prior.Text != text || checkReplayGeneration && prior.Generation >= 0 && prior.Generation != selectionRevision) { throw new InvalidOperationException("This request ID belongs to another submission."); }
            if (prior.Status == "pending") { await Publish(new ComposerSubmitted(FullKey, prior.Id)); }
            return prior.Id;
        }
        await ValidateSelection(context, selectionRevision);
        if (value != Current.Draft) { throw new InvalidOperationException("The draft changed. Review it and send again."); }
        if (text.Length == 0) { return null; }
        if (Current.Submissions.FirstOrDefault(request => request.Context == context && request.Generation == Current.Generation && request.DraftVersion == Current.DraftVersion && request.Status == "pending") is { } pending)
        {
            await Publish(new ComposerSubmitted(FullKey, pending.Id));
            return pending.Id;
        }
        if (Current.Submissions.Count(request => request.Status == "pending") >= 16) { throw new InvalidOperationException("Too many messages are waiting. Try again when a submission finishes."); }
        var request = new ComposerSubmission(requestId, context, text, Current.DraftVersion, Generation: Current.Generation);
        // Keep all unacknowledged input even if an older request is slower than later ones.
        var retained = Current.Submissions.Where(item => item.Status == "pending")
            .Concat(Current.Submissions.Where(item => item.Status != "pending").TakeLast(64)).ToArray();
        await Save(Current with { Submissions = [.. retained, request], Error = null }, Snapshot.Audio,
            new ComposerSubmitted(FullKey, requestId));
        return requestId;
    }

    public async Task Settle(Guid requestId, string? error = null)
    {
        var request = Current.Submissions.FirstOrDefault(item => item.Id == requestId);
        if (request is null || request.Status != "pending") { return; }
        var sameDraft = request.Context == Current.Context && request.Generation == Current.Generation && request.DraftVersion == Current.DraftVersion;
        var accepted = error is null;
        await Save(Current with
        {
            Submissions = [.. Current.Submissions.Select(item => item.Id == requestId ? item with { Status = accepted ? "accepted" : "refused", Error = error } : item)],
            Draft = accepted && sameDraft ? "" : Current.Draft,
            DraftVersion = accepted && sameDraft ? Current.DraftVersion + 1 : Current.DraftVersion,
            Error = request.Context == Current.Context && request.Generation == Current.Generation ? error : Current.Error,
            Voice = accepted && sameDraft ? null : Current.Voice,
        }, accepted && sameDraft ? null : Snapshot.Audio);
    }

    public Task Defer(Guid requestId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var request = Current.Submissions.FirstOrDefault(item => item.Id == requestId);
        if (request is null || request.Status != "pending") { return Task.CompletedTask; }
        return Save(Current with
        {
            Submissions = [.. Current.Submissions.Select(item => item.Id == requestId ? item with { Error = error } : item)],
            Error = request.Context == Current.Context && request.Generation == Current.Generation ? error : Current.Error,
        }, Snapshot.Audio);
    }

    public Task BeginVoice(Guid captureId, string context)
    {
        RequireStandalone();
        return BeginVoiceCapture(captureId, context);
    }

    public async Task BeginVoice(Guid captureId, string context, long selectionRevision)
    {
        await ValidateSelection(context, selectionRevision);
        await BeginVoiceCapture(captureId, context);
    }

    private Task BeginVoiceCapture(Guid captureId, string context)
    {
        ValidateId(captureId);
        RequireContext(context);
        if (Current.Voice?.Id == captureId) { return Task.CompletedTask; }
        return Save(Current with { Voice = new(captureId, context, Current.DraftVersion, "recording"), Error = null }, null);
    }

    public Task CaptureVoice(Guid captureId, byte[] audio, string mimeType)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        if (audio.Length is 0 or > IVoiceInput.MaxAudioBytes) { throw new ArgumentException("Audio must contain between 1 byte and 8 MiB."); }
        if (!mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) { throw new ArgumentException("Only audio can be captured."); }
        if (Current.Voice is not { } voice || voice.Id != captureId) { return Task.CompletedTask; }
        if (voice.Status == "transcribing") { return Publish(new ComposerVoiceCaptured(FullKey, captureId)); }
        if (voice.Status != "recording") { return Task.CompletedTask; }
        return Save(Current with { Voice = voice with { Status = "transcribing" } }, new([.. audio], mimeType.Trim().ToLowerInvariant()),
            new ComposerVoiceCaptured(FullKey, captureId));
    }

    public Task CancelVoice(Guid captureId)
    {
        if (Current.Voice is not { } voice || voice.Id != captureId || voice.Status is not ("recording" or "transcribing")) { return Task.CompletedTask; }
        return Save(Current with { Voice = voice with { Status = "cancelled" } }, null);
    }

    public Task CompleteVoice(Guid captureId, string? text, string? error = null)
    {
        if (Current.Voice is not { } voice || voice.Id != captureId || voice.Status is not ("recording" or "transcribing")) { return Task.CompletedTask; }
        if (error is null && voice.Status != "transcribing") { throw new InvalidOperationException("Audio has not been captured."); }
        if (error is null && string.IsNullOrWhiteSpace(text)) { error = "No speech was detected. Try recording again."; }
        if (error is null && text!.Length > 32000) { error = "The transcript is too long. Try a shorter recording."; }
        var applies = error is null && voice.Context == Current.Context && voice.DraftVersion == Current.DraftVersion;
        return Save(Current with
        {
            Draft = applies ? text!.Trim() : Current.Draft,
            DraftVersion = applies ? Current.DraftVersion + 1 : Current.DraftVersion,
            Voice = voice with
            {
                Status = error is not null ? "failed" : applies ? "ready" : "discarded",
                Error = error ?? (applies ? null : "Your draft changed; the transcription was not applied. Record again when ready."),
            },
        }, null);
    }

    public Task<ComposerAudio?> ReadAudio(Guid captureId) => Task.FromResult(Current.Voice is { Status: "transcribing" } voice && voice.Id == captureId ? Snapshot.Audio : null);
    public Task<ComposerState> Read() => Task.FromResult(Current);

    private void RequireContext(string context)
    {
        if (string.IsNullOrWhiteSpace(Current.Context) || context != Current.Context) { throw new InvalidOperationException("The conversation changed. Review the current draft before continuing."); }
    }

    private static void ValidateText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 32000) { throw new ArgumentException("A draft has at most 32000 characters."); }
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) { throw new ArgumentException("An operation needs a unique ID."); }
    }

    // Every write announces ComposerChanged, with the signal that says what was acted on when there is one.
    private Task Save(ComposerState state, ComposerAudio? audio, DigitalBrain.Signal? signal = null)
    {
        var next = state with { Name = FullKey, Revision = Current.Revision + 1 };
        return Save(new ComposerStorage { State = next, Audio = audio }, new ComposerChanged(next.Name, next.Revision), signal);
    }
}
