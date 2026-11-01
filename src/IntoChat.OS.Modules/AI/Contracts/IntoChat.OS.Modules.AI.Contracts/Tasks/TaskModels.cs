using System;

namespace IntoChat.OS.Modules.AI.Tasks;

public enum TaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4,
    Cancelled = 5
}

public enum TaskStepStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4
}

public sealed record TaskStepDefinition(
    string Title,
    string Description,
    string Capability,
    string? RollbackCapability = null);

public sealed record CreateTaskRequest(
    string Title,
    string Description,
    string Owner,
    TaskStepDefinition[] Steps);

public sealed record TaskStep(
    int Index,
    string Title,
    string Description,
    string Capability,
    TaskStepStatus Status,
    string? RollbackCapability = null,
    string? Output = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null);

public sealed record AgentTask(
    string TaskId,
    string Title,
    string Description,
    string Owner,
    TaskStatus Status,
    TaskStep[] Steps,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null,
    string? RollbackReason = null);

public sealed record TaskVerificationResult(
    bool Passed,
    string Summary,
    string[]? Violations = null);
