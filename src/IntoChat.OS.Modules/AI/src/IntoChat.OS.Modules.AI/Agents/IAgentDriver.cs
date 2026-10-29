using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Agents;

// Start hands the turn to this one-way self-call, so the caller's request ends while the turn runs on the
// agent's own scheduler (a deferred turn would hold the caller: the host drains deferred work before a call
// returns). Interleaving lets Read and Cancel reach the agent meanwhile. Module plumbing: no edge names it.
[NeuronAlias("ai.agent-driver")]
internal interface IAgentDriver
{
    [DigitalBrain.OneWayCall, DigitalBrain.Interleaves] Task Drive(AgentTurn turn, string runId);

    // Activation closes a left-open run and announces it here, once activation is done. It only
    // publishes, so it interleaves rather than waiting behind a new turn.
    [DigitalBrain.OneWayCall, DigitalBrain.Interleaves] Task Interrupted(string runId, string error);
}
