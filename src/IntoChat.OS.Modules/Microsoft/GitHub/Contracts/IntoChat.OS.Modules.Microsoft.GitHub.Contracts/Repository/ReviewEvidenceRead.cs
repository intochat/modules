namespace IntoChat.OS.Modules.Microsoft.GitHub;

public sealed record ReviewEvidenceRead(
    PullRequestSnapshot Current,
    GitHubReviewEvidence? Evidence,
    string? Detail = null);
