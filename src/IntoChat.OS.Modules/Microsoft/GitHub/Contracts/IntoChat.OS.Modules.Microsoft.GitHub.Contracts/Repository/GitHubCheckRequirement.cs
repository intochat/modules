namespace IntoChat.OS.Modules.Microsoft.GitHub;

public sealed record GitHubCheckRequirement(string Name,
    long? AppId = null, string Kind = "check");
