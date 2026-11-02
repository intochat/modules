namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.merge-pull-request")]
public sealed record MergePullRequest(
    [property: Id(0)] int PullRequestNumber,
    [property: Id(1)] string ExpectedHeadSha,
    [property: Id(2)] string MergeMethod = "squash",
    [property: Id(3)] string? CommitTitle = null,
    [property: Id(4)] string? CommitMessage = null);
