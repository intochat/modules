using DigitalBrain.Access;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

// The connection neuron's grain type is its connector id, "github".
[DigitalBrain.Contracts.NeuronAlias("github.repository"), DigitalBrain.Contracts.DefaultNeuronType("github")]
[Shareable]
public interface IRepository
{
    Task<RepositoryView> Refresh(RefreshRepository request);

    [DigitalBrain.ReadOnlyCall]
    Task<RepositoryView> Read();

    [DigitalBrain.ReadOnlyCall]
    Task<PullRequestRead> ReadPullRequest(ReadPullRequest query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<PullRequestsRead> ReadPullRequests();

    [DigitalBrain.ReadOnlyCall]
    Task<ReviewEvidenceRead> ReadReviewEvidence(ReadReviewEvidence query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<RequiredChecksRead> ReadRequiredChecks(CancellationToken cancellationToken = default);

    Task PushBranch(PushBranchRequest request);

    Task<PullRequestView> CreatePullRequest(CreatePullRequest request);

    Task ReplyToReviewComment(ReplyReviewComment request);

    Task MergePullRequest(MergePullRequest request);
}
