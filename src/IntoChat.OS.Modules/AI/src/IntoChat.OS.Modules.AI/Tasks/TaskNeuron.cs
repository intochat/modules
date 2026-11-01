using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalBrain;
using IntoChat.OS.Framework.Diagnostics;
using IntoChat.OS.Modules.AI.Tasks.Signals;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.AI.Tasks;

[Neuron("tasks.task", State = "task")]
public sealed class TaskNeuron : Neuron<TaskState>, ITaskNeuron
{
    private ITaskDirectoryNeuron Directory => Get<ITaskDirectoryNeuron>(new NeuronId(Id.Brain, "tasks.directory", "default"));

    public async Task<AgentTask> InitializeAsync(CreateTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskId = Id.Key;
        var steps = new TaskStepState[request.Steps.Length];
        for (var i = 0; i < request.Steps.Length; i++)
        {
            var def = request.Steps[i];
            steps[i] = new TaskStepState
            {
                Index = i,
                Title = def.Title,
                Description = def.Description,
                Capability = def.Capability,
                Status = TaskStepStatus.Pending,
                RollbackCapability = def.RollbackCapability,
            };
        }

        var next = new TaskState
        {
            TaskId = taskId,
            Title = request.Title,
            Description = request.Description,
            Owner = request.Owner,
            Status = TaskStatus.Pending,
            Steps = steps,
            CreatedAt = Context.Time.GetUtcNow(),
        };

        await Save(next, new TaskCreated(next.TaskId, next.Title, next.Owner));

        try
        {
            await Directory.RegisterTaskAsync(next.ToModel());
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory registration");
        }

        return next.ToModel();
    }

    public Task<AgentTask> GetAsync()
    {
        return Task.FromResult(State.ToModel());
    }

    public async Task<AgentTask> AdvanceStepAsync(int stepIndex, TaskStepStatus newStatus, string? output = null)
    {
        if (stepIndex < 0 || stepIndex >= State.Steps.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(stepIndex), $"Step {stepIndex} is out of bounds (0-{State.Steps.Length - 1}).");
        }

        var now = Context.Time.GetUtcNow();
        var nextSteps = State.Steps.Select(s => new TaskStepState
        {
            Index = s.Index,
            Title = s.Title,
            Description = s.Description,
            Capability = s.Capability,
            Status = s.Status,
            RollbackCapability = s.RollbackCapability,
            Output = s.Output,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt,
        }).ToArray();

        var targetStep = nextSteps[stepIndex];
        targetStep.Status = newStatus;
        if (newStatus == TaskStepStatus.Running && targetStep.StartedAt is null)
        {
            targetStep.StartedAt = now;
        }
        else if (newStatus == TaskStepStatus.Completed || newStatus == TaskStepStatus.Failed)
        {
            targetStep.CompletedAt = now;
            targetStep.Output = output;
        }

        var nextTaskStatus = State.Status;
        DateTimeOffset? completedAt = State.CompletedAt;

        if (newStatus == TaskStepStatus.Failed)
        {
            nextTaskStatus = TaskStatus.Failed;
            completedAt = now;
        }
        else if (nextSteps.Length > 0 && nextSteps.All(s => s.Status == TaskStepStatus.Completed))
        {
            nextTaskStatus = TaskStatus.Completed;
            completedAt = now;
        }
        else if ((newStatus == TaskStepStatus.Running || newStatus == TaskStepStatus.Completed)
                 && (nextTaskStatus == TaskStatus.Pending || nextTaskStatus == TaskStatus.RolledBack))
        {
            nextTaskStatus = TaskStatus.Running;
        }

        var next = new TaskState
        {
            TaskId = State.TaskId,
            Title = State.Title,
            Description = State.Description,
            Owner = State.Owner,
            Status = nextTaskStatus,
            Steps = nextSteps,
            CreatedAt = State.CreatedAt,
            CompletedAt = completedAt,
            RollbackReason = State.RollbackReason,
        };

        var signals = new List<Signal>
        {
            new TaskStepAdvanced(next.TaskId, stepIndex, newStatus)
        };

        if (nextTaskStatus == TaskStatus.Completed || nextTaskStatus == TaskStatus.Failed)
        {
            signals.Add(new TaskCompleted(next.TaskId, nextTaskStatus));
        }

        await Save(next, signals.ToArray());

        try
        {
            await Directory.UpdateTaskStatusAsync(next.TaskId, next.Status, next.CompletedAt);
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory update on step advance");
        }

        return next.ToModel();
    }

    public Task<TaskVerificationResult> VerifyStepAsync(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= State.Steps.Length)
        {
            return Task.FromResult(new TaskVerificationResult(false, $"Step index {stepIndex} is invalid.", ["Out of bounds."]));
        }

        var target = State.Steps[stepIndex];
        if (target.Status == TaskStepStatus.Failed)
        {
            return Task.FromResult(new TaskVerificationResult(false, $"Step {stepIndex} failed.", [target.Output ?? "Execution failed."]));
        }

        for (var i = 0; i < stepIndex; i++)
        {
            if (State.Steps[i].Status != TaskStepStatus.Completed)
            {
                return Task.FromResult(new TaskVerificationResult(false, $"Prior step {i} is not completed (status: {State.Steps[i].Status}).", [$"Step {i} barrier violation."]));
            }
        }

        return Task.FromResult(new TaskVerificationResult(true, $"Step {stepIndex} verified successfully.", []));
    }

    public async Task<AgentTask> RollbackAsync(int toStepIndex, string reason)
    {
        if (toStepIndex < 0 || toStepIndex >= State.Steps.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(toStepIndex));
        }

        var nextSteps = State.Steps.Select(s => new TaskStepState
        {
            Index = s.Index,
            Title = s.Title,
            Description = s.Description,
            Capability = s.Capability,
            Status = s.Status,
            RollbackCapability = s.RollbackCapability,
            Output = s.Output,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt,
        }).ToArray();

        for (var i = nextSteps.Length - 1; i >= toStepIndex; i--)
        {
            if (nextSteps[i].Status == TaskStepStatus.Completed || nextSteps[i].Status == TaskStepStatus.Running || nextSteps[i].Status == TaskStepStatus.Failed)
            {
                nextSteps[i].Status = TaskStepStatus.RolledBack;
            }
        }

        var next = new TaskState
        {
            TaskId = State.TaskId,
            Title = State.Title,
            Description = State.Description,
            Owner = State.Owner,
            Status = TaskStatus.RolledBack,
            Steps = nextSteps,
            CreatedAt = State.CreatedAt,
            CompletedAt = State.CompletedAt,
            RollbackReason = reason,
        };

        await Save(next, new TaskRolledBack(next.TaskId, toStepIndex, reason));

        try
        {
            await Directory.UpdateTaskStatusAsync(next.TaskId, next.Status, next.CompletedAt);
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory update on rollback");
        }

        return next.ToModel();
    }

    public async Task<AgentTask> CancelAsync(string reason)
    {
        var now = Context.Time.GetUtcNow();
        var next = new TaskState
        {
            TaskId = State.TaskId,
            Title = State.Title,
            Description = State.Description,
            Owner = State.Owner,
            Status = TaskStatus.Cancelled,
            Steps = State.Steps,
            CreatedAt = State.CreatedAt,
            CompletedAt = now,
            RollbackReason = reason,
        };

        await Save(next, new TaskCompleted(next.TaskId, TaskStatus.Cancelled));

        try
        {
            await Directory.UpdateTaskStatusAsync(next.TaskId, next.Status, next.CompletedAt);
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory update on cancel");
        }

        return next.ToModel();
    }
}
