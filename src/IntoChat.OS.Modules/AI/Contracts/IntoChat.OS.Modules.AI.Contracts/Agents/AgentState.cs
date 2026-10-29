using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Agents;

public enum AgentRunStatus { Running, Completed, Failed, Cancelled, Interrupted }

public sealed record AgentUsage(
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens);

// A tool call the run started: Result when it completed, Code and Error when it did not.
public sealed record AgentToolCall(
    string CallId,
    string Name,
    string Arguments,
    string? Result = null,
    string? Code = null,
    string? Error = null);

// One turn of the agent, from the user's text to the reply or the reason there is none.
public sealed record AgentRun(
    string RunId,
    AgentRunStatus Status,
    string UserText,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null,
    string? Reply = null,
    ResolvedModel? Model = null,
    AgentUsage? Usage = null,
    string? Error = null)
{
    public AgentToolCall[] ToolCalls { get; init; } = [];
    // Recorded by IAgent.Post: an assistant-only entry with no user turn and no model call.
    public bool Posted { get; init; }
}

// Transcript is read through IAgent.Transcript; it is too large to ride along on every read.
public sealed record AgentState(
    long Revision,
    AgentDefinition Definition,
    string? Summary,
    AgentRun? ActiveRun,
    AgentRun[] Runs)
{
    // Revision guards configuration; Version orders every persisted conversation change.
    public long Version { get; init; }
}
