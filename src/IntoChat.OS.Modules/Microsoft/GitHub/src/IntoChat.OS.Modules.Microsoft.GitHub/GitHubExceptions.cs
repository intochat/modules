namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.unavailable")]
internal sealed class GitHubUnavailableException(string message) : InvalidOperationException(message);

[GenerateSerializer, Alias("github.access-denied")]
internal sealed class GitHubAccessDeniedException(string message) : InvalidOperationException(message);
