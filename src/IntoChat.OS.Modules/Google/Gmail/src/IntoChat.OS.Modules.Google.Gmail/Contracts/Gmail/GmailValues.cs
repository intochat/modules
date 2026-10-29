namespace IntoChat.OS.Modules.Google.Gmail;

[GenerateSerializer, Alias("gmail.field-issue")]
public sealed record GmailFieldIssue(
    [property: Id(0)] string Path,
    [property: Id(1)] string Code,
    [property: Id(2)] string Message);

[GenerateSerializer, Alias("gmail.failure")]
public sealed record GmailFailure(
    [property: Id(0)] string Code,
    [property: Id(1)] string Message,
    [property: Id(2)] GmailFieldIssue[] Violations,
    [property: Id(3)] DateTimeOffset? RetryAt = null);

[GenerateSerializer, Alias("gmail.message-summary")]
public sealed record GmailMessageSummary(
    [property: Id(0)] string MessageId,
    [property: Id(1)] string ThreadId,
    [property: Id(2)] string? From,
    [property: Id(3)] string? Subject,
    [property: Id(4)] DateTimeOffset? ReceivedAt,
    [property: Id(5)] string Snippet,
    [property: Id(6)] bool Unread);

// MissingMessageIds identifies messages that disappeared between listing and metadata reads.
[GenerateSerializer, Alias("gmail.inbox-page")]
public sealed record GmailInboxPage(
    [property: Id(0)] GmailMessageSummary[] Messages,
    [property: Id(1)] string? NextPageToken,
    [property: Id(2)] string[] MissingMessageIds);

// Reason is attachment_data_not_fetched, unsupported_body_type, traversal_limit or size_limit.
[GenerateSerializer, Alias("gmail.body-omission")]
public sealed record GmailBodyOmission(
    [property: Id(0)] string PartId,
    [property: Id(1)] string? MediaType,
    [property: Id(2)] string Reason);

// Bodies share a 256 KiB decoded UTF-8 limit. A missing body with an omission differs from an empty body.
[GenerateSerializer, Alias("gmail.message")]
public sealed record GmailMessage(
    [property: Id(0)] GmailMessageSummary Summary,
    [property: Id(1)] string[] To,
    [property: Id(2)] string[] Cc,
    [property: Id(3)] string? TextBody,
    [property: Id(4)] string? HtmlBody,
    [property: Id(5)] bool BodyTruncated,
    [property: Id(6)] GmailBodyOmission[] BodyOmissions,
    [property: Id(7)] bool HasAttachments);

// Local preparation only: the preview is bound to its creator and mailbox, and expires after ten minutes.
[GenerateSerializer, Alias("gmail.prepared-draft")]
public sealed record GmailPreparedDraft(
    [property: Id(0)] string PreviewId,
    [property: Id(1)] string[] To,
    [property: Id(2)] string[] Cc,
    [property: Id(3)] string[] Bcc,
    [property: Id(4)] string Subject,
    [property: Id(5)] string Body,
    [property: Id(6)] DateTimeOffset ExpiresAt);

public enum GmailDraftOperationStatus { Pending = 0, Completed = 1, Failed = 2, Unknown = 3 }
public enum GmailAccountingStatus { Pending = 0, Settled = 1, CancelledBeforeDispatch = 2 }

[GenerateSerializer, Alias("gmail.draft-accounting")]
public sealed record GmailDraftAccounting(
    [property: Id(0)] string TraceId,
    [property: Id(1)] string InvocationId,
    [property: Id(2)] GmailAccountingStatus Status);

// Unknown must be inspected, never automatically retried. A missing expired receipt does not prove no draft exists.
// A completed provider draft may still have pending accounting; neither fact erases the other.
[GenerateSerializer, Alias("gmail.draft-operation")]
public sealed record GmailDraftOperation(
    [property: Id(0)] string RequestId,
    [property: Id(1)] string PreviewId,
    [property: Id(2)] string Mailbox,
    [property: Id(3)] GmailDraftOperationStatus Status,
    [property: Id(4)] string? DraftId,
    [property: Id(5)] GmailFailure? Failure,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] DateTimeOffset UpdatedAt,
    [property: Id(8)] DateTimeOffset? RetainUntil,
    [property: Id(9)] GmailDraftAccounting? Accounting = null);
