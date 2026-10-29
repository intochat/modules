namespace IntoChat.OS.Modules.Microsoft.GitHub;

internal interface IGitHubRepositorySource
{
    Task<PullRequestSnapshot> GetPullRequestAsync(GitHubRepository repository, int number, CancellationToken cancellationToken);
    Task<IReadOnlyList<PullRequestSnapshot>> ListOpenPullRequestsAsync(GitHubRepository repository, CancellationToken cancellationToken);
    Task<GitHubReviewEvidence> GetReviewEvidenceAsync(GitHubRepository repository, PullRequestSnapshot snapshot, CancellationToken cancellationToken);
    Task<RequiredChecksRead> GetRequiredChecksAsync(GitHubRepository repository, string? branch, CancellationToken cancellationToken) => Task.FromResult(new RequiredChecksRead([], false, "Required CI checks have not been discovered for this source."));
}
