using System;

namespace IntoChat.OS.Modules.AI.Tasks;

public enum TaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4,
    Cancelled = 5,
    AwaitingConfirmation = 6
}

public enum TaskStepStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4,
    AwaitingConfirmation = 5
}

public sealed record TaskLink(
    string TargetId,
    string Kind);

public sealed record TaskStepDefinition(
    string Title,
    string Description,
    string Capability,
    string? RollbackCapability = null,
    string? IdempotencyKey = null);

public sealed record CreateTaskRequest(
    string Title,
    string Description,
    string Owner,
    TaskStepDefinition[] Steps,
    string? ParentId = null,
    TaskLink[]? Links = null);

public sealed record TaskStep(
    int Index,
    string Title,
    string Description,
    string Capability,
    TaskStepStatus Status,
    string? RollbackCapability = null,
    string? Output = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    string? IdempotencyKey = null);

public sealed record AgentTask(
    string TaskId,
    string Title,
    string Description,
    string Owner,
    TaskStatus Status,
    TaskStep[] Steps,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null,
    string? RollbackReason = null,
    string? ParentId = null,
    TaskLink[]? Links = null);

public sealed record TaskVerificationResult(
    bool Passed,
    string Summary,
    string[]? Violations = null);

public static class BotIdentity
{
    public const string Login = "intochat-bot[bot]";

    public static bool IsBot(string? author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return false;
        }

        return string.Equals(author, Login, StringComparison.OrdinalIgnoreCase)
            || author.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(author, "github-actions[bot]", StringComparison.OrdinalIgnoreCase);
    }
}
