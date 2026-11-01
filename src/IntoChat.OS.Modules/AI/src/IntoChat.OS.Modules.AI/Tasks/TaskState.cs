using System;

namespace IntoChat.OS.Modules.AI.Tasks;

[GenerateSerializer, Alias("tasks.state")]
public sealed class TaskState
{
    [Id(0)] public string TaskId { get; set; } = "";
    [Id(1)] public string Title { get; set; } = "";
    [Id(2)] public string Description { get; set; } = "";
    [Id(3)] public string Owner { get; set; } = "";
    [Id(4)] public TaskStatus Status { get; set; } = TaskStatus.Pending;
    [Id(5)] public TaskStepState[] Steps { get; set; } = [];
    [Id(6)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(7)] public DateTimeOffset? CompletedAt { get; set; }
    [Id(8)] public string? RollbackReason { get; set; }

    public AgentTask ToModel()
    {
        var steps = new TaskStep[Steps.Length];
        for (var i = 0; i < Steps.Length; i++)
        {
            var s = Steps[i];
            steps[i] = new TaskStep(
                s.Index,
                s.Title,
                s.Description,
                s.Capability,
                s.Status,
                s.RollbackCapability,
                s.Output,
                s.StartedAt,
                s.CompletedAt);
        }

        return new AgentTask(
            TaskId,
            Title,
            Description,
            Owner,
            Status,
            steps,
            CreatedAt,
            CompletedAt,
            RollbackReason);
    }
}

[GenerateSerializer, Alias("tasks.step-state")]
public sealed class TaskStepState
{
    [Id(0)] public int Index { get; set; }
    [Id(1)] public string Title { get; set; } = "";
    [Id(2)] public string Description { get; set; } = "";
    [Id(3)] public string Capability { get; set; } = "";
    [Id(4)] public TaskStepStatus Status { get; set; } = TaskStepStatus.Pending;
    [Id(5)] public string? RollbackCapability { get; set; }
    [Id(6)] public string? Output { get; set; }
    [Id(7)] public DateTimeOffset? StartedAt { get; set; }
    [Id(8)] public DateTimeOffset? CompletedAt { get; set; }
}

[GenerateSerializer, Alias("tasks.directory-state")]
public sealed class TaskDirectoryState
{
    [Id(0)] public TaskDirectoryEntryState[] Entries { get; set; } = [];
}

[GenerateSerializer, Alias("tasks.directory-entry")]
public sealed class TaskDirectoryEntryState
{
    [Id(0)] public string TaskId { get; set; } = "";
    [Id(1)] public string Title { get; set; } = "";
    [Id(2)] public string Description { get; set; } = "";
    [Id(3)] public string Owner { get; set; } = "";
    [Id(4)] public TaskStatus Status { get; set; } = TaskStatus.Pending;
    [Id(5)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(6)] public DateTimeOffset? CompletedAt { get; set; }
    [Id(7)] public int StepCount { get; set; }
    [Id(8)] public int CompletedStepCount { get; set; }
}
