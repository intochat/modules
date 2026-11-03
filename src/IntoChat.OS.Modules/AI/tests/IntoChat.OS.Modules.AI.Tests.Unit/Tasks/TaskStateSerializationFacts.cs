using System;
using System.Reflection;
using System.Text.Json;
using DigitalBrain;
using DigitalBrain.Invocation;
using IntoChat.OS.Modules.AI.Tasks;
using IntoChat.OS.Modules.AI.Tasks.Signals;
using Xunit;
using TaskStatus = IntoChat.OS.Modules.AI.Tasks.TaskStatus;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Tasks;

public sealed class TaskStateSerializationFacts
{
    [Fact]
    public void TaskState_RoundTripsThroughRuntimeJson()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new TaskState
        {
            TaskId = "task-alpha-123",
            Title = "Model Training",
            Description = "Train specialized LoRA",
            Owner = "researcher",
            Status = TaskStatus.Running,
            CreatedAt = now.AddMinutes(-5),
            CompletedAt = null,
            RollbackReason = null,
            ParentId = "task-parent-001",
            Links =
            [
                new TaskLinkState { TargetId = "local/apps/notes", Kind = "produced" }
            ],
            Steps =
            [
                new TaskStepState
                {
                    Index = 0,
                    Title = "Data Prep",
                    Description = "Clean tokens",
                    Capability = "data.clean",
                    Status = TaskStepStatus.Completed,
                    StartedAt = now.AddMinutes(-5),
                    CompletedAt = now.AddMinutes(-3),
                    Output = "400000 tokens",
                    RollbackCapability = "data.revert"
                },
                new TaskStepState
                {
                    Index = 1,
                    Title = "Fine Tune",
                    Description = "Run epochs",
                    Capability = "gpu.train",
                    Status = TaskStepStatus.Running,
                    StartedAt = now.AddMinutes(-3),
                    CompletedAt = null,
                    Output = null,
                    RollbackCapability = null
                }
            ]
        };

        var json = JsonSerializer.Serialize(state, RuntimeProtocol.Json);
        var read = JsonSerializer.Deserialize<TaskState>(json, RuntimeProtocol.Json)!;

        Assert.Equal(state.TaskId, read.TaskId);
        Assert.Equal(state.Title, read.Title);
        Assert.Equal(state.Description, read.Description);
        Assert.Equal(state.Owner, read.Owner);
        Assert.Equal(state.Status, read.Status);
        Assert.Equal(state.CreatedAt, read.CreatedAt);
        Assert.Null(read.CompletedAt);
        Assert.Equal("task-parent-001", read.ParentId);
        Assert.Single(read.Links);
        Assert.Equal("local/apps/notes", read.Links[0].TargetId);
        Assert.Equal("produced", read.Links[0].Kind);
        Assert.Equal(2, read.Steps.Length);
        Assert.Equal(state.Steps[0].Title, read.Steps[0].Title);
        Assert.Equal(state.Steps[0].Status, read.Steps[0].Status);
        Assert.Equal(state.Steps[0].Output, read.Steps[0].Output);
        Assert.Equal(state.Steps[1].Title, read.Steps[1].Title);
        Assert.Equal(state.Steps[1].Status, read.Steps[1].Status);
    }

    [Fact]
    public void TaskDirectoryState_RoundTripsThroughRuntimeJson()
    {
        var state = new TaskDirectoryState
        {
            Entries =
            [
                new TaskDirectoryEntryState
                {
                    TaskId = "task-1",
                    Title = "Ingest",
                    Owner = "alice",
                    Status = TaskStatus.Completed,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    CompletedAt = DateTimeOffset.UtcNow,
                    StepCount = 3,
                    CompletedStepCount = 3
                },
                new TaskDirectoryEntryState
                {
                    TaskId = "task-2",
                    Title = "Export",
                    Owner = "bob",
                    Status = TaskStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CompletedAt = null,
                    StepCount = 2,
                    CompletedStepCount = 0
                }
            ]
        };

        var json = JsonSerializer.Serialize(state, RuntimeProtocol.Json);
        var read = JsonSerializer.Deserialize<TaskDirectoryState>(json, RuntimeProtocol.Json)!;

        Assert.Equal(2, read.Entries.Length);
        Assert.Equal("task-1", read.Entries[0].TaskId);
        Assert.Equal(TaskStatus.Completed, read.Entries[0].Status);
        Assert.Equal("task-2", read.Entries[1].TaskId);
        Assert.Equal(TaskStatus.Pending, read.Entries[1].Status);
    }

    [Fact]
    public void TaskStepAdvanced_HasEphemeralSignalAttribute()
    {
        var attr = typeof(TaskStepAdvanced).GetCustomAttribute<EphemeralSignalAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void TaskSignals_RoundTripThroughRuntimeJson()
    {
        var stepAdvanced = new TaskStepAdvanced("task-123", 2, TaskStepStatus.Completed);
        var json = JsonSerializer.Serialize(stepAdvanced, RuntimeProtocol.Json);
        var read = JsonSerializer.Deserialize<TaskStepAdvanced>(json, RuntimeProtocol.Json)!;

        Assert.Equal("task-123", read.TaskId);
        Assert.Equal(2, read.StepIndex);
        Assert.Equal(TaskStepStatus.Completed, read.Status);
    }
}
