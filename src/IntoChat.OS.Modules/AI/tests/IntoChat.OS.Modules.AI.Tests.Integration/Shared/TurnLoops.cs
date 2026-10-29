using IntoChat.OS.Modules.AI.Agents;
using IntoChat.OS.Modules.AI.Inference;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

// The agent's turn loop over a hand-built host, for facts about a transport
// rather than about the agent neuron that persists the turn.
internal static class TurnLoops
{
    public static async Task<List<AgentTurnEvent>> Collect(IServiceProvider services, string message, string[] contracts, CancellationToken ct)
    {
        var plan = new TurnPlan("scope", "scope/agents/loop", "run", [], null, new AiMessage("user", [new AiText(message)]), "", contracts, [], null, null, 16, false);
        var events = new List<AgentTurnEvent>();
        await foreach (var item in new AgentTurnLoop(services).Run(plan, ct)) { events.Add(item); }
        return events;
    }
}
