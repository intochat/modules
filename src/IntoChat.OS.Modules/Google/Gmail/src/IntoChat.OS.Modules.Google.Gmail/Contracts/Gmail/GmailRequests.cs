namespace IntoChat.OS.Modules.Google.Gmail;

[GenerateSerializer, Alias("gmail.inbox-request")]
public sealed record GmailInboxRequest(
    [property: Id(0)] int PageSize = 25,
    [property: Id(1)] string? PageToken = null);

[GenerateSerializer, Alias("gmail.message-request")]
public sealed record GmailMessageRequest([property: Id(0)] string MessageId);

[GenerateSerializer, Alias("gmail.draft-request")]
public sealed record GmailDraftRequest(
    [property: Id(0)] string[] To,
    [property: Id(1)] string[] Cc,
    [property: Id(2)] string[] Bcc,
    [property: Id(3)] string Subject,
    [property: Id(4)] string Body);

[GenerateSerializer, Alias("gmail.create-draft-request")]
public sealed record GmailCreateDraftRequest(
    [property: Id(0)] string PreviewId,
    [property: Id(1)] string RequestId);

[GenerateSerializer, Alias("gmail.draft-operation-request")]
public sealed record GmailDraftOperationRequest([property: Id(0)] string RequestId);
