namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.push-branch-request")]
public sealed record PushBranchRequest(
    [property: Id(0)] string Branch,
    [property: Id(1)] string CommitSha,
    [property: Id(2)] bool Force = false);
