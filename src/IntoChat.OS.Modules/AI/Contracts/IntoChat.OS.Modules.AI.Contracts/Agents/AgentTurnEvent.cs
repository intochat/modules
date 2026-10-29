using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Agents;

// Plain calls stream the events as JSON (06-06d), so the abstract record names its cases.
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$event")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(Started), "started")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModelSelected), "model-selected")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(Text), "text")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ToolStarted), "tool-started")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ToolCompleted), "tool-completed")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ToolFailed), "tool-failed")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(Completed), "completed")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(Finished), "finished")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(Failed), "failed")]
public abstract record AgentTurnEvent
{
    public sealed record Started(string RunId) : AgentTurnEvent;
    public sealed record ModelSelected(ResolvedModel Model, ModelDescriptor Descriptor) : AgentTurnEvent;
    public sealed record Text(string Content) : AgentTurnEvent;
    public sealed record ToolStarted(string CallId, string Name, string Arguments) : AgentTurnEvent;
    public sealed record ToolCompleted(string CallId, string Name, string Result) : AgentTurnEvent;
    public sealed record ToolFailed(string CallId, string Name,
        string Code = "tool_failed", string Message = "The tool call failed.") : AgentTurnEvent;
    public sealed record Completed(IReadOnlyList<AiMessage> Messages, AgentUsage? Usage) : AgentTurnEvent;
    public sealed record Finished : AgentTurnEvent;
    public sealed record Failed(string Message, bool Cancelled = false) : AgentTurnEvent;
}
