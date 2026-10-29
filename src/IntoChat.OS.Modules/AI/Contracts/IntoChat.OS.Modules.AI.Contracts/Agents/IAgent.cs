using DigitalBrain.Contracts;
using IntoChat.OS.Modules.AI.Inference;

namespace IntoChat.OS.Modules.AI.Agents;

// The agent keeps the transcript and runs its own turn. Run streams the turn; Start answers the
// run id at once and reports the turn as signals. Both publish the same signals.
// A plain contract since 06-06d: every call interleaves, as it did on the grain (reads see only committed versions).
[NeuronAlias("agent"), DefaultNeuronType("agent")]
public interface IAgent
{
    [DigitalBrain.Interleaves] Task Configure(AgentDefinition definition, long expectedRevision);
    [DigitalBrain.Interleaves, DigitalBrain.CallTimeout(600_000)] IAsyncEnumerable<AgentTurnEvent> Run(AgentTurn turn, CancellationToken ct = default);
    [DigitalBrain.Interleaves] Task<string> Start(AgentTurn turn);
    [DigitalBrain.Interleaves] Task Cancel();
    [DigitalBrain.Interleaves] Task CancelRun(string runId);
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<AgentState> Read();
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<AiMessage[]> Transcript();
    [DigitalBrain.Interleaves] Task Clear();
    // Records a completed assistant-only entry (no model call), e.g. a greeting; one entry per id
    // (stored as run `post/{id}`). Throws InvalidOperationException while a run is active: retry later.
    [DigitalBrain.Interleaves] Task Post(string text, string id);
}
