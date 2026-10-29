namespace IntoChat.OS.Modules.Microsoft.GitHub;

public sealed record RequiredChecksRead(
    IReadOnlyList<GitHubCheckRequirement> Checks,
    bool Complete,
    string? Detail = null);
