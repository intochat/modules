using IntoChat.OS.Connections.Platform.Connections;

namespace IntoChat.OS.Modules.Google.Gmail;

[GenerateSerializer, Alias("db.gmail.state")]
internal sealed class GmailState : ConnectionState
{
    [Id(8)] public bool CanCompose { get; set; }
    [Id(9)] public GmailDraftPreview? PendingDraft { get; set; }
    [Id(10)] public GmailDraftPreview? SubmittingDraft { get; set; }
    [Id(11)] public long GrantGeneration { get; set; }
    [Id(12)] public string? BoundMailbox { get; set; }
    [Id(13)] public byte[] PageTokenKey { get; set; } = [];
    [Id(14)] public GmailStoredPreview[] DraftPreviews { get; set; } = [];
    [Id(15)] public GmailStoredOperation[] DraftOperations { get; set; } = [];
    [Id(20)] public GmailWatchState? Watch { get; set; }
    [Id(21)] public GmailHistoryState? History { get; set; }

    // Added aggregates are immutable; replace them on a copy so a failed write restores the old state.
    internal GmailState Copy() => (GmailState)MemberwiseClone();
}

[GenerateSerializer, Alias("gmail.stored-preview")]
internal sealed record GmailStoredPreview(
    [property: Id(0)] GmailPreparedDraft Value,
    [property: Id(1)] string CallerScope,
    [property: Id(2)] string Mailbox,
    [property: Id(3)] long GrantGeneration,
    [property: Id(4)] string Digest,
    [property: Id(5)] string? RequestId = null);

[GenerateSerializer, Alias("gmail.stored-operation")]
internal sealed record GmailStoredOperation(
    [property: Id(0)] GmailDraftOperation Value,
    [property: Id(1)] string CallerScope,
    [property: Id(2)] long GrantGeneration,
    [property: Id(3)] string Digest);
