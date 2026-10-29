using IntoChat.OS.Connections.Platform.Connections;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.repository-state")]
internal sealed class GitHubRepositoryState : ConnectionState
{
    [Id(8)] public long AppId { get; set; }
    [Id(9)] public long InstallationId { get; set; }
    [Id(10)] public long RepositoryId { get; set; }
    [Id(11)] public string Owner { get; set; } = "";
    [Id(12)] public string Name { get; set; } = "";
    [Id(13)] public PullRequestSnapshot[] PullRequests { get; set; } = [];
    [Id(14)] public string[] ProcessedDeliveries { get; set; } = [];
    [Id(15)] public DateTimeOffset? LastWebhookAt { get; set; }
}
