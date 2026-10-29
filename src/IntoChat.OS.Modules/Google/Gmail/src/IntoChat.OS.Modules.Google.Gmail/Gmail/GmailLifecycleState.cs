namespace IntoChat.OS.Modules.Google.Gmail;

internal enum GmailWatchStatus { Unconfigured = 0, Active = 1, RetryPending = 2, Stopped = 3 }

[GenerateSerializer, Alias("gmail.watch-state")]
internal sealed record GmailWatchState(
    [property: Id(0)] string Mailbox,
    [property: Id(1)] long GrantGeneration,
    [property: Id(2)] string? TopicName,
    [property: Id(3)] DateTimeOffset? ExpiresAt,
    [property: Id(4)] DateTimeOffset? NextActionAt,
    [property: Id(5)] GmailWatchStatus Status);

[GenerateSerializer, Alias("gmail.history-state")]
internal sealed record GmailHistoryState(
    [property: Id(0)] string Mailbox,
    [property: Id(1)] long GrantGeneration,
    [property: Id(2)] string? Cursor,
    [property: Id(3)] GmailHistoryWork? Work,
    [property: Id(4)] GmailHistoryWakeup? Wakeup,
    [property: Id(5)] bool ResyncRequired,
    [property: Id(6)] DateTimeOffset? RetryAt);

[GenerateSerializer, Alias("gmail.history-work")]
internal sealed record GmailHistoryWork(
    [property: Id(0)] string StartHistoryId,
    [property: Id(1)] string TargetHistoryId,
    [property: Id(2)] string? PageToken,
    [property: Id(3)] Trace Trace,
    [property: Id(4)] string InvocationId);

[GenerateSerializer, Alias("gmail.history-wakeup")]
internal sealed record GmailHistoryWakeup(
    [property: Id(0)] string HistoryId,
    [property: Id(1)] Trace Trace);
