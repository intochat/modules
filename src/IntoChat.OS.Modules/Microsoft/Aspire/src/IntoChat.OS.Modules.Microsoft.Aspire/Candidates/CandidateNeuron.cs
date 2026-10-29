using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.Aspire.Verification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

// A plain neuron since 06-06d. The index, the live cluster (still a grain) and the candidates are reached through the brain's
// in-process client, as before.
[DigitalBrain.Neuron("aspire.candidate")]
internal sealed class CandidateNeuron(ICandidateHost host, ICandidateVerifier verifier, ILogger<CandidateNeuron> log)
    : DigitalBrain.Neuron<CandidateState>, ICandidate
{
    private CandidateState Snapshot => State;
    private IServiceProvider ServiceProvider => Context.Services;
    private string Key => $"{Id.Brain.Value}/{Id.Key}";
    private string Sha => Key[(Key.LastIndexOf('/') + 1)..];
    private string BrainId => BrainKey.BrainOf(Key);
    private T Peer<T>(string relative) where T : class
        => Get<T>(IntoChat.OS.Runtime.PlainNeuronContracts.IdOf(typeof(T), $"{Id.Brain.Value}/{relative}"));
    private ICandidates Index => Peer<ICandidates>(ICandidates.Relative);
    private IDeployment LiveCluster => Peer<IDeployment>(IDeployment.Relative);

    public Task<CandidateSnapshot> Read() => Task.FromResult(Describe());

    public async Task<CandidateSnapshot> Propose(string branch, string summary, VerificationPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentNullException.ThrowIfNull(plan);
        if (Snapshot.History.Length > 0) { throw new InvalidOperationException($"Candidate {Sha} is already {Snapshot.Stage}."); }
        await Move(CandidateStage.Proposed, new CandidateState { Branch = branch, Summary = summary, Plan = plan }, null);
        return Describe();
    }

    public async Task<CandidateSnapshot> Queue()
    {
        Require(CandidateStage.Proposed);
        await Move(CandidateStage.Queued, Snapshot, null);
        return Describe();
    }

    public async Task<CandidateSnapshot> Start()
    {
        Require(CandidateStage.Proposed, CandidateStage.Queued);
        var plan = Snapshot.Plan!;
        try
        {
            await Move(CandidateStage.Building, Snapshot, null);
            await host.BuildGate(Sha, CancellationToken.None);
            await host.Publish(Sha, CancellationToken.None);
            await Move(CandidateStage.Starting, Snapshot, null);
            var running = await host.Run(Sha, CancellationToken.None);
            await Move(CandidateStage.Starting, Snapshot with { Endpoints = running.Endpoints }, new CandidateStarted(Sha, running.Endpoints));
            await host.WaitHealthy(Sha, plan.Budget, CancellationToken.None);
            await Move(CandidateStage.Healthy, Snapshot, new CandidateHealthy(Sha));
        }
        catch (Exception error)
        {
            await Fail(error.Message, removeContainer: Snapshot.Stage == CandidateStage.Starting);
        }
        return Describe();
    }

    public async Task<CandidateSnapshot> Verify()
    {
        Require(CandidateStage.Healthy);
        var plan = Snapshot.Plan!;
        await Move(CandidateStage.Verifying, Snapshot, null);
        using var budget = new CancellationTokenSource(plan.Budget);
        VerificationReport report;
        try { report = await verifier.Verify(BrainId, Sha, Snapshot.Endpoints!, plan, budget.Token); }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            await Fail($"budget {plan.Budget} exceeded", removeContainer: true);
            return Describe();
        }
        catch (Exception error) when (error is InvalidOperationException or HttpRequestException)
        {
            await Fail(error.Message, removeContainer: true);
            return Describe();
        }
        if (report.Passed) { await Move(CandidateStage.Verified, Snapshot with { Report = report }, new CandidateVerified(Sha, report)); }
        else
        {
            var failed = report.Checks.First(check => !check.Passed);
            await Fail($"{failed.Name}: {failed.Detail}", removeContainer: true, Snapshot with { Report = report });
        }
        return Describe();
    }

    // One-way, so nobody observes a throw: anything Start could not turn into Failed is logged here.
    public async Task Begin()
    {
        if (Snapshot.Stage is not (CandidateStage.Proposed or CandidateStage.Queued)) { return; }
        try { await Start(); }
        catch (Exception error) { log.LogError(error, "Candidate {Candidate} could not start.", Key); }
    }

    public async Task<CandidateSnapshot> Promote()
    {
        if (Snapshot.Stage is CandidateStage.Promoting or CandidateStage.Promoted) { return Describe(); }
        if (Snapshot.Stage != CandidateStage.Verified) { throw new InvalidOperationException($"Candidate {Sha} is {Snapshot.Stage}; only a Verified candidate can be promoted."); }
        await Move(CandidateStage.Promoting, Snapshot, null);
        string? previous;
        try { previous = await host.Commit(Sha, CancellationToken.None); }
        catch (Exception error)
        {
            await Fail($"commit: {error.Message}", removeContainer: true);
            return Describe();
        }
        // The silos restart onto the build after Restart returns, this one among them: finishing here would race
        // its own shutdown (seen live: Remove killed mid-way). The new build's cluster activation calls Finish.
        DeploymentSnapshot cluster;
        try { cluster = await LiveCluster.Restart(Sha); }
        catch (Exception error)
        {
            // A restart the cluster still heads for is settled by its next activation; any other is not happening.
            if (Snapshot.Stage != CandidateStage.Promoting) { return Describe(); }
            if (await LiveCluster.Read() is { Stage: DeploymentStage.Restarting } heading && heading.TargetSha == Sha) { return Describe(); }
            await Uncommitted(previous);
            await Fail($"restart: {error.Message}", removeContainer: true);
            return Describe();
        }
        return cluster.Stage == DeploymentStage.Steady && cluster.CurrentSha == Sha ? await Finish() : Describe();
    }

    public async Task<CandidateSnapshot> Finish()
    {
        if (Snapshot.Stage != CandidateStage.Promoting || settling) { return Describe(); }
        settling = true;
        try
        {
            await Move(CandidateStage.Promoted, Snapshot, new CandidatePromoted(Sha));
            await Removed();
            await Index.Ended(Sha);
        }
        finally { settling = false; }
        return Describe();
    }

    public async Task<CandidateSnapshot> Abandon(string reason)
    {
        if (Snapshot.Stage != CandidateStage.Promoting || settling) { return Describe(); }
        settling = true;
        try
        {
            await Uncommitted(null);
            await Fail(reason, removeContainer: true);
        }
        finally { settling = false; }
        return Describe();
    }

    // Finish and Abandon interleave with each other and with Promote: one settles the promotion.
    private bool settling;

    public async Task<CandidateSnapshot> Discard()
    {
        if (Snapshot.Stage is CandidateStage.Promoted or CandidateStage.Discarded) { return Describe(); }
        await Move(CandidateStage.Discarded, Snapshot, null);
        if (Snapshot.Endpoints is not null) { await Removed(); }
        await Index.Ended(Sha);
        return Describe();
    }

    private void Require(params CandidateStage[] stages)
    {
        if (!stages.Contains(Snapshot.Stage)) { throw new InvalidOperationException($"Candidate {Sha} is {Snapshot.Stage}; expected {string.Join(" or ", stages)}."); }
    }

    // The failed signal names the stage the candidate was in. Failed is recorded first; removing the
    // containers is best effort after it, so a failing docker never leaves the candidate in its old stage.
    private async Task Fail(string reason, bool removeContainer, CandidateState? from = null)
    {
        var failedAt = Snapshot.Stage;
        try { await Move(CandidateStage.Failed, (from ?? Snapshot) with { Failure = reason }, new CandidateFailed(Sha, failedAt, reason)); }
        finally
        {
            if (removeContainer) { await Removed(); }
            await Index.Ended(Sha);
        }
    }

    private async Task Removed()
    {
        try { await host.Remove(Sha, CancellationToken.None); }
        catch (Exception error) { log.LogWarning(error, "Candidate {Candidate} containers could not be removed; the cluster's orphan sweep retries.", Key); }
    }

    // Puts the live ref back when a promotion did not take. previous is unknown after a restart (Abandon):
    // the ref then goes back to this candidate's parent, the build it was prepared on.
    private async Task Uncommitted(string? previous)
    {
        try { await host.Uncommit(Sha, previous, CancellationToken.None); }
        catch (Exception error) { log.LogWarning(error, "Candidate {Candidate} could not put the live ref back.", Key); }
    }

    private Task Move(CandidateStage stage, CandidateState next, DigitalBrain.Signal? acted)
        => Save(next with { Stage = stage, History = [.. next.History, stage] }, new CandidateChanged(Sha, stage), acted);

    private CandidateSnapshot Describe()
        => new(Sha, Snapshot.Branch, Snapshot.Summary, Snapshot.Stage, Snapshot.Endpoints, Snapshot.Plan, Snapshot.Report, Snapshot.Failure, Snapshot.History);
}
