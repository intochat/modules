namespace IntoChat.OS.Modules.Microsoft.GitHub;

public sealed record PullRequestsRead(
    IReadOnlyList<PullRequestSnapshot> Snapshots,
    bool Available);
