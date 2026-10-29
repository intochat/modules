namespace IntoChat.OS.Modules.Google.Gmail;

[GenerateSerializer, Alias("gmail.inbox-result")]
public sealed record GmailInboxResult
{
    public GmailInboxResult(GmailInboxPage? value, GmailFailure? failure)
    {
        if ((value is null) == (failure is null)) { throw new ArgumentException("Exactly one value or failure is required."); }
        Value = value;
        Failure = failure;
    }

    [Id(0)] public GmailInboxPage? Value { get; }
    [Id(1)] public GmailFailure? Failure { get; }
    public static GmailInboxResult FromValue(GmailInboxPage value) => new(value, null);
    public static GmailInboxResult FromFailure(GmailFailure failure) => new(null, failure);
}

[GenerateSerializer, Alias("gmail.message-result")]
public sealed record GmailMessageResult
{
    public GmailMessageResult(GmailMessage? value, GmailFailure? failure)
    {
        if ((value is null) == (failure is null)) { throw new ArgumentException("Exactly one value or failure is required."); }
        Value = value;
        Failure = failure;
    }

    [Id(0)] public GmailMessage? Value { get; }
    [Id(1)] public GmailFailure? Failure { get; }
    public static GmailMessageResult FromValue(GmailMessage value) => new(value, null);
    public static GmailMessageResult FromFailure(GmailFailure failure) => new(null, failure);
}

[GenerateSerializer, Alias("gmail.draft-preview-result")]
public sealed record GmailDraftPreviewResult
{
    public GmailDraftPreviewResult(GmailPreparedDraft? value, GmailFailure? failure)
    {
        if ((value is null) == (failure is null)) { throw new ArgumentException("Exactly one value or failure is required."); }
        Value = value;
        Failure = failure;
    }

    [Id(0)] public GmailPreparedDraft? Value { get; }
    [Id(1)] public GmailFailure? Failure { get; }
    public static GmailDraftPreviewResult FromValue(GmailPreparedDraft value) => new(value, null);
    public static GmailDraftPreviewResult FromFailure(GmailFailure failure) => new(null, failure);
}

[GenerateSerializer, Alias("gmail.draft-creation-result")]
public sealed record GmailDraftCreationResult
{
    public GmailDraftCreationResult(GmailDraftOperation? value, GmailFailure? failure)
    {
        if ((value is null) == (failure is null)) { throw new ArgumentException("Exactly one value or failure is required."); }
        Value = value;
        Failure = failure;
    }

    [Id(0)] public GmailDraftOperation? Value { get; }
    [Id(1)] public GmailFailure? Failure { get; }
    public static GmailDraftCreationResult FromValue(GmailDraftOperation value) => new(value, null);
    public static GmailDraftCreationResult FromFailure(GmailFailure failure) => new(null, failure);
}

[GenerateSerializer, Alias("gmail.draft-operation-result")]
public sealed record GmailDraftOperationResult
{
    public GmailDraftOperationResult(GmailDraftOperation? value, GmailFailure? failure)
    {
        if ((value is null) == (failure is null)) { throw new ArgumentException("Exactly one value or failure is required."); }
        Value = value;
        Failure = failure;
    }

    [Id(0)] public GmailDraftOperation? Value { get; }
    [Id(1)] public GmailFailure? Failure { get; }
    public static GmailDraftOperationResult FromValue(GmailDraftOperation value) => new(value, null);
    public static GmailDraftOperationResult FromFailure(GmailFailure failure) => new(null, failure);
}
