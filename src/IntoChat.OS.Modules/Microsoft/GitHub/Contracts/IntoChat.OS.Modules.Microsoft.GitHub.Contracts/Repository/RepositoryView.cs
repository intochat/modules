namespace IntoChat.OS.Modules.Microsoft.GitHub;

public sealed record RepositoryView(
    string ConnectionId,
    bool Connected,
    IReadOnlyList<PullRequestSnapshot> PullRequests,
    DateTimeOffset? LastWebhookAt);
