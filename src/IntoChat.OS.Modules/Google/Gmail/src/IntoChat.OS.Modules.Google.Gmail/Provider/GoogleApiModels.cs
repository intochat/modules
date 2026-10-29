using IntoChat.OS.Contracts.Compute;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed record GoogleCallContext(Funding Funding, CostSubject Subject, string InvocationId);

internal enum GoogleApiFailureKind
{
    InvalidRequest, ReconsentRequired, Forbidden, NotFound, RateLimited, Unavailable, InvalidResponse,
}

internal sealed record GoogleApiFailure(GoogleApiFailureKind Kind, int? StatusCode = null,
    DateTimeOffset? RetryAt = null, bool Retryable = false);

internal sealed record GoogleApiResult<T> where T : class
{
    public T? Value { get; }
    public GoogleApiFailure? Failure { get; }
    public ExternalCallCompletion? Accounting { get; }

    public GoogleApiResult(T? value, GoogleApiFailure? failure, ExternalCallCompletion? accounting)
    {
        if ((value is null) == (failure is null)) { throw new ArgumentException("Exactly one value or failure is required."); }
        Value = value;
        Failure = failure;
        Accounting = accounting;
    }
}

internal sealed record GoogleProfile(string EmailAddress, string HistoryId);
internal sealed record GoogleWatch(string HistoryId, DateTimeOffset ExpiresAt);
internal sealed record GoogleHistoryPage(string HistoryId, string? NextPageToken, int RecordCount);
internal sealed record GoogleUnit
{
    public static GoogleUnit Value { get; } = new();
}
internal sealed record GoogleMessageReference(string Id, string ThreadId);
internal sealed record GoogleMessagePage(GoogleMessageReference[] Messages, string? NextPageToken);
internal sealed record GoogleMessageMetadata(string Id, string ThreadId, string[] LabelIds,
    DateTimeOffset? ReceivedAt, string? Subject, string? From, string? To, string? Cc, string? Snippet);
internal sealed record GoogleBodyPart(string MimeType, string Content);
internal sealed record GoogleBodyOmission(string PartId, string? MediaType, string Reason);
internal sealed record GoogleMessageContent(GoogleMessageMetadata Metadata, GoogleBodyPart[] BodyParts,
    int ReturnedBodyBytes, bool Truncated, bool HasAttachments, GoogleBodyOmission[] Omissions);

internal enum GoogleWriteOutcome { Succeeded, Rejected, Unknown, NotDispatched }
internal sealed record GoogleDraftWriteResult(GoogleWriteOutcome Outcome, string? DraftId = null,
    string? MessageId = null, GoogleApiFailure? Failure = null, ExternalCallCompletion? Accounting = null,
    bool CancellationRequested = false);
