namespace IntoChat.OS.Modules.Microsoft.GitHub;

public sealed record PullRequestSnapshot(
    int Number,
    string Title,
    string Url,
    bool IsOpen,
    bool IsDraft,
    string HeadSha,
    string BaseSha,
    string? MergeSha,
    string CiSha,
    GitHubCheck[] Checks,
    bool ChecksComplete,
    DateTimeOffset ObservedAt,
    DateTimeOffset CreatedAt,
    string Revision,
    string CiRevision,
    long RepositoryId = 0,
    string? BaseBranch = null);

public sealed record GitHubCheck(
    string Name,
    long? AppId,
    string Kind,
    string State,
    string? Conclusion,
    string Sha,
    string AttemptId,
    DateTimeOffset UpdatedAt);

public sealed record GitHubReviewEvidence(
    string HeadSha,
    string BaseSha,
    string Text,
    string Hash,
    bool Complete);
