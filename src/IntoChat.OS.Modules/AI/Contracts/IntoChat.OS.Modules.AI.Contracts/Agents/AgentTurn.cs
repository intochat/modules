using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Agents;

// One request to the agent. RunId is caller-chosen for replay (at most 200 characters) or null
// for a fresh id. Model, Instructions and Contracts and Contexts override the definition for this turn only.
// Intent names the metering intent; null meters under the run id.
public sealed record AgentTurn(
    AiMessage Message,
    string? RunId = null,
    ModelSelection? Model = null,
    string? Instructions = null,
    string[]? Contracts = null,
    string? Intent = null,
    string[]? Contexts = null)
{
    public static AgentTurn Text(string message, string? runId = null, string? model = null, string? instructions = null, string[]? contracts = null, string[]? contexts = null)
        => new(new AiMessage("user", [new AiText(message)]), runId, model is { Length: > 0 } ? new(model) : null, instructions, contracts, Contexts: contexts);
}
