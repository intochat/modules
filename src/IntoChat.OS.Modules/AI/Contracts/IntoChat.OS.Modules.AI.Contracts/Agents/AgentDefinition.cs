using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Agents;

// Contracts are full type names of the neuron contracts the model may act on.
// Contexts are IAgentContext neuron keys the agent reads at the start of every turn.
public sealed record AgentDefinition
{
    public string DisplayName { get; init; } = "Assistant";
    public string Description { get; init; } = "";
    public string Instructions { get; init; } = "";
    public ModelSelection? Model { get; init; }
    public string[] Contracts { get; init; } = [];
    public string[] Capabilities { get; init; } = [];
    public string[] RoutingExamples { get; init; } = [];
    public int MaxModelCalls { get; init; } = 16;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
    public int MaxHistoryMessages { get; init; } = 256;
    public InferenceOptions? Options { get; init; }
    public string[] Contexts { get; init; } = [];
}
