namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.create-pull-request")]
public sealed record CreatePullRequest(
    [property: Id(0)] string Title,
    [property: Id(1)] string HeadBranch,
    [property: Id(2)] string BaseBranch = "main",
    [property: Id(3)] string Body = "",
    [property: Id(4)] bool Draft = false);
