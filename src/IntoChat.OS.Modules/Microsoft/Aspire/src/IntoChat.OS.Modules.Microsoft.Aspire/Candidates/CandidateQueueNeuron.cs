using IntoChat.OS.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

// At most one candidate is live per brain; the rest wait here in proposal order.
// A plain neuron since 06-06d.
[DigitalBrain.Neuron("aspire.candidates")]
internal sealed class CandidateQueueNeuron(ICandidateHost host) : DigitalBrain.Neuron<CandidateQueueState>, ICandidates
{
    private CandidateQueueState Snapshot => State;
    private string BrainId => Id.Brain.Value;
    private T Peer<T>(string relative) where T : class
        => Get<T>(IntoChat.OS.Runtime.PlainNeuronContracts.IdOf(typeof(T), $"{BrainId}/{relative}"));

    public async Task<string> Propose(CandidateProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        await Heal();
        var cluster = await Peer<IDeployment>(IDeployment.Relative).Read();
        var prepared = await host.Prepare(cluster.CurrentSha, proposal.Summary, proposal.Files, CancellationToken.None);
        var candidate = Candidate(prepared.Sha);
        await candidate.Propose(proposal.Branch, proposal.Summary, proposal.Plan);
        if (Snapshot.Live is null)
        {
            await Save(Snapshot with { Live = prepared.Sha }, new CandidateChanged(prepared.Sha, CandidateStage.Building));
            await candidate.Begin();
        }
        else
        {
            await Save(Snapshot with { Queue = [.. Snapshot.Queue, prepared.Sha] }, new CandidateChanged(prepared.Sha, CandidateStage.Queued));
            await candidate.Queue();
        }
        return prepared.Sha;
    }

    public async Task Heal()
    {
        foreach (var sha in Snapshot.Queue)
        {
            if (Ended((await Candidate(sha).Read()).Stage)) { await Ended(sha); }
        }
        if (Snapshot.Live is not { } live) { return; }
        var stage = (await Candidate(live).Read()).Stage;
        if (Ended(stage)) { await Ended(live); }
        // Begin is a no-op past Queued, so a Begin that did arrive and is still building is not repeated.
        else if (stage is CandidateStage.Proposed or CandidateStage.Queued) { await Candidate(live).Begin(); }
    }

    private static bool Ended(CandidateStage stage) => stage is CandidateStage.Promoted or CandidateStage.Discarded or CandidateStage.Failed;

    private ICandidate Candidate(string sha) => Peer<ICandidate>(ICandidate.Relative(sha));

    public Task<string?> Live() => Task.FromResult(Snapshot.Live);

    public Task<string[]> Known() => Task.FromResult(Snapshot.Live is null ? Snapshot.Queue : [Snapshot.Live, .. Snapshot.Queue]);

    public async Task Ended(string sha)
    {
        if (Snapshot.Queue.Contains(sha))
        {
            await Save(Snapshot with { Queue = [.. Snapshot.Queue.Where(queued => queued != sha)] }, new CandidateChanged(sha, CandidateStage.Discarded));
            return;
        }
        if (Snapshot.Live != sha) { return; }
        var next = Snapshot.Queue.FirstOrDefault();
        await Save(Snapshot with { Live = next, Queue = next is null ? [] : Snapshot.Queue[1..] }, new CandidateChanged(sha, CandidateStage.Discarded));
        if (next is not null) { await Candidate(next).Begin(); }
    }
}
