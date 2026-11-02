namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.pull-request-view")]
public sealed record PullRequestView(
    [property: Id(0)] int Number,
    [property: Id(1)] string Title,
    [property: Id(2)] string Url,
    [property: Id(3)] string HeadSha,
    [property: Id(4)] string BaseSha,
    [property: Id(5)] bool IsOpen,
    [property: Id(6)] bool IsDraft,
    [property: Id(7)] string? MergeSha = null);
