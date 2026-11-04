using DigitalBrain.Capabilities.Connections;
using DigitalBrain.Runtime.Orleans.Capabilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Capabilities;
using DigitalBrain.Confirmation;
using DigitalBrain.Spi;
using IntoChat.OS.Modules.Microsoft.GitHub.Signals;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntoChat.OS.Modules.Microsoft.GitHub.Tests.Unit;

public sealed class GitHubRepositoryWritesFacts
{
    [Fact]
    public void PullRequestChanged_PropertiesAndDefaults()
    {
        var defaultSignal = new PullRequestChanged(42, "rev:ciRev", "subject-key");
        Assert.Equal(42, defaultSignal.Number);
        Assert.Equal("rev:ciRev", defaultSignal.Version);
        Assert.Equal("subject-key", defaultSignal.SubjectKey);
        Assert.Null(defaultSignal.CiStatus);
        Assert.Null(defaultSignal.CommentAuthor);
        Assert.Null(defaultSignal.CommentBody);

        var detailedSignal = new PullRequestChanged(42, "rev:ciRev", "subject-key", "failure", "reviewer-1", "Needs changes");
        Assert.Equal("failure", detailedSignal.CiStatus);
        Assert.Equal("reviewer-1", detailedSignal.CommentAuthor);
        Assert.Equal("Needs changes", detailedSignal.CommentBody);
    }

    [Fact]
    public async Task PushBranch_InvokesSourceAndPassesParameters()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        var request = new PushBranchRequest("feature-x", "0123456789012345678901234567890123456789", true);

        await neuron.PushBranch(request);

        Assert.NotNull(mockSource.LastPushBranchRequest);
        Assert.Equal("feature-x", mockSource.LastPushBranchRequest.Branch);
        Assert.Equal("0123456789012345678901234567890123456789", mockSource.LastPushBranchRequest.CommitSha);
        Assert.True(mockSource.LastPushBranchRequest.Force);
    }

    [Fact]
    public async Task CreatePullRequest_InvokesSourceAndReturnsView()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        var expectedView = new PullRequestView(10, "Feature 10", "https://github.com/intochat/os/pull/10", "sha-head", "sha-base", true, false, null);
        mockSource.NextPullRequestView = expectedView;

        var request = new CreatePullRequest("Feature 10", "feature-x", "main", "description", false);
        var result = await neuron.CreatePullRequest(request);

        Assert.Equal(expectedView, result);
        Assert.NotNull(mockSource.LastCreatePullRequest);
        Assert.Equal("Feature 10", mockSource.LastCreatePullRequest.Title);
        Assert.Equal("feature-x", mockSource.LastCreatePullRequest.HeadBranch);
    }

    [Fact]
    public async Task ReplyToReviewComment_InvokesSourceWithCommentIdAndBody()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        var request = new ReplyReviewComment(42, 9999, "Fixed in latest commit");

        await neuron.ReplyToReviewComment(request);

        Assert.NotNull(mockSource.LastReplyReviewComment);
        Assert.Equal(42, mockSource.LastReplyReviewComment.PullRequestNumber);
        Assert.Equal(9999, mockSource.LastReplyReviewComment.CommentId);
        Assert.Equal("Fixed in latest commit", mockSource.LastReplyReviewComment.Body);
    }

    [Fact]
    public async Task MergePullRequest_ThrowsWhenCallerIsUnstamped()
    {
        var (neuron, _) = CreateConnectedNeuron();
        var request = new MergePullRequest(1, "head-sha-1234", "squash");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => neuron.MergePullRequest(request));
    }

    [Fact]
    public async Task MergePullRequest_ThrowsWhenCallerIsUnauthorizedApp()
    {
        var (neuron, _) = CreateConnectedNeuron();
        var request = new MergePullRequest(1, "head-sha-1234", "squash");

        using (RuntimeCallerContext.StampScope(CapabilityCallers.Of("app-99", "acc-1", "local", CallerKind.App, "random.app")))
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => neuron.MergePullRequest(request));
        }
    }

    [Fact]
    public async Task MergePullRequest_AllowsUserCaller()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        mockSource.SnapshotToReturn = CreateSnapshot(1, "head-sha-1234");
        var request = new MergePullRequest(1, "head-sha-1234", "squash");

        using (RuntimeCallerContext.StampScope(CapabilityCallers.Of("user-1", "acc-1", "local", CallerKind.User)))
        {
            await neuron.MergePullRequest(request);
        }

        Assert.NotNull(mockSource.LastMergePullRequest);
        Assert.Equal(1, mockSource.LastMergePullRequest.PullRequestNumber);
        Assert.Equal("head-sha-1234", mockSource.LastMergePullRequest.ExpectedHeadSha);
    }

    [Fact]
    public async Task MergePullRequest_AllowsPlatformCaller()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        mockSource.SnapshotToReturn = CreateSnapshot(1, "head-sha-1234");
        var request = new MergePullRequest(1, "head-sha-1234", "squash");

        using (RuntimeCallerContext.StampScope(CapabilityCallers.Of("platform", "acc-1", "local", CallerKind.Platform)))
        {
            await neuron.MergePullRequest(request);
        }

        Assert.NotNull(mockSource.LastMergePullRequest);
    }

    [Fact]
    public async Task MergePullRequest_AllowsTaskNeuronCaller()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        mockSource.SnapshotToReturn = CreateSnapshot(1, "head-sha-1234");
        var request = new MergePullRequest(1, "head-sha-1234", "squash");

        using (RuntimeCallerContext.StampScope(CapabilityCallers.Of("task-123", "acc-1", "local", CallerKind.App, "tasks.task")))
        {
            await neuron.MergePullRequest(request);
        }

        Assert.NotNull(mockSource.LastMergePullRequest);
    }

    [Fact]
    public async Task MergePullRequest_ThrowsInvalidOperation_WhenExpectedHeadShaMismatchesRemoteHead()
    {
        var (neuron, mockSource) = CreateConnectedNeuron();
        mockSource.SnapshotToReturn = CreateSnapshot(1, "new-remote-head-5678");
        var request = new MergePullRequest(1, "old-expected-head-1234", "squash");

        using (RuntimeCallerContext.StampScope(CapabilityCallers.Of("user-1", "acc-1", "local", CallerKind.User)))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => neuron.MergePullRequest(request));
            Assert.Contains("does not match expected HEAD SHA", ex.Message);
        }

        Assert.Null(mockSource.LastMergePullRequest);
    }

    private static (GitHubRepositoryNeuron neuron, MockGitHubRepositorySource source) CreateConnectedNeuron()
    {
        var mockSource = new MockGitHubRepositorySource();
        var neuron = new GitHubRepositoryNeuron(mockSource, null!, null!, null!);

        var context = new TestNeuronContext(
            new NeuronId(new BrainId("local"), "github", "connection/github/main"),
            new GitHubRepositoryState
            {
                Status = ConnectionStatus.Connected,
                RepositoryId = 12345,
                Owner = "intochat",
                Name = "os",
                Account = "intochat/os",
            });

        typeof(Neuron).GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(neuron, [context]);
        ((Task)typeof(Neuron).GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(neuron, null)!).GetAwaiter().GetResult();

        return (neuron, mockSource);
    }

    private static PullRequestSnapshot CreateSnapshot(int number, string headSha)
    {
        return new PullRequestSnapshot(
            number,
            "Test PR",
            "https://github.com/intochat/os/pull/" + number,
            true,
            false,
            headSha,
            "base-sha-0000",
            null,
            headSha,
            [],
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "rev-1",
            "cirev-1",
            12345,
            "main");
    }

    private sealed class MockGitHubRepositorySource : IGitHubRepositorySource
    {
        public PushBranchRequest? LastPushBranchRequest { get; private set; }
        public CreatePullRequest? LastCreatePullRequest { get; private set; }
        public ReplyReviewComment? LastReplyReviewComment { get; private set; }
        public MergePullRequest? LastMergePullRequest { get; private set; }

        public PullRequestView? NextPullRequestView { get; set; }
        public PullRequestSnapshot? SnapshotToReturn { get; set; }

        public Task<PullRequestSnapshot> GetPullRequestAsync(GitHubRepository repository, int number, CancellationToken cancellationToken)
        {
            return Task.FromResult(SnapshotToReturn ?? CreateSnapshot(number, "default-head"));
        }

        public Task<IReadOnlyList<PullRequestSnapshot>> ListOpenPullRequestsAsync(GitHubRepository repository, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PullRequestSnapshot>>([]);
        }

        public Task<GitHubReviewEvidence> GetReviewEvidenceAsync(GitHubRepository repository, PullRequestSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.FromResult(new GitHubReviewEvidence(snapshot.HeadSha, snapshot.BaseSha, "", "", true));
        }

        public Task<RequiredChecksRead> GetRequiredChecksAsync(GitHubRepository repository, string? branch, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RequiredChecksRead([], true, null));
        }

        public Task PushBranchAsync(GitHubRepository repository, PushBranchRequest request, CancellationToken cancellationToken)
        {
            LastPushBranchRequest = request;
            return Task.CompletedTask;
        }

        public Task<PullRequestView> CreatePullRequestAsync(GitHubRepository repository, CreatePullRequest request, CancellationToken cancellationToken)
        {
            LastCreatePullRequest = request;
            var view = NextPullRequestView ?? new PullRequestView(1, request.Title, "https://github.com/pr/1", "head", "base", true, request.Draft, null);
            return Task.FromResult(view);
        }

        public Task ReplyToReviewCommentAsync(GitHubRepository repository, ReplyReviewComment request, CancellationToken cancellationToken)
        {
            LastReplyReviewComment = request;
            return Task.CompletedTask;
        }

        public Task MergePullRequestAsync(GitHubRepository repository, MergePullRequest request, CancellationToken cancellationToken)
        {
            LastMergePullRequest = request;
            return Task.CompletedTask;
        }
    }

    private sealed class TestNeuronContext : INeuronContext
    {
        private readonly object _state;

        public TestNeuronContext(NeuronId id, object state)
        {
            Id = id;
            _state = state;
            Services = new ServiceCollection().BuildServiceProvider();
            Time = TimeProvider.System;
            State = new TestNeuronState(_state);
            Timers = new TestTimers();
            Reminders = new TestReminders();
        }

        public NeuronId Id { get; }
        public IServiceProvider Services { get; }
        public TimeProvider Time { get; }
        public INeuronState State { get; }
        public ITimers Timers { get; }
        public IReminders Reminders { get; }
        public IOperations Operations => null!;
        public ISynapses Synapses => null!;
        public ISignalHistory History => null!;
        public IConfirmations Confirmations => null!;

        public Task Publish(DigitalBrain.Signal signal) => Task.CompletedTask;
        public Task PublishEphemeral(DigitalBrain.Signal signal) => Task.CompletedTask;
        public T Get<T>(NeuronId id) where T : class => typeof(T) == typeof(IPolicies) ? (T)(object)AllowAllPolicies.Instance : null!;
        public void Defer(Func<Task> work) { }
        public Task<ConfirmationResult> Confirm(ConfirmationPlan plan, CancellationToken ct = default)
            => Task.FromResult(new ConfirmationResult(plan.Id, ConfirmationOutcome.Approved, ConfirmationDenyReason.None, Skipped: true));
    }

    private sealed class AllowAllPolicies : IPolicies
    {
        public static readonly AllowAllPolicies Instance = new();

        public Task<ProposalOutcome> Propose(PolicyRule rule, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PolicyDecision> Allow(string action, decimal amount, IReadOnlyDictionary<string, string>? facts, CancellationToken ct = default)
            => Task.FromResult(new PolicyDecision(true, "allow", null, null));
        public Task Release(string decisionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task Retire(string ruleId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PolicyRule>> List(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PolicyRule>>([]);
        public Task<IReadOnlyList<ActionDescriptor>> Actions(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ActionDescriptor>>([]);
    }

    private sealed class TestNeuronState : INeuronState
    {
        private object _current;

        public TestNeuronState(object initial) => _current = initial;

        public Task<T?> Load<T>() => Task.FromResult((T?)_current);

        public Task Save<T>(T state, IReadOnlyList<DigitalBrain.Signal> signals)
        {
            _current = state!;
            return Task.CompletedTask;
        }

        public Task Clear() => Task.CompletedTask;
    }

    private sealed class TestTimers : ITimers
    {
        public void Start(string name, TimeSpan due, TimeSpan? period) { }
        public void Start(string name, TimeSpan due, TimeSpan? period, bool keepAlive) { }
        public void Stop(string name) { }
    }

    private sealed class TestReminders : IReminders
    {
        public Task Start(string name, TimeSpan due, TimeSpan period) => Task.CompletedTask;
        public Task Stop(string name) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> Names() => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
