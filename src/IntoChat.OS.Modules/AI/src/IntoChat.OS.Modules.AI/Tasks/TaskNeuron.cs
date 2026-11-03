using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using IntoChat.OS.Framework.Diagnostics;
using IntoChat.OS.Modules.AI.Tasks.Signals;
using IntoChat.OS.Modules.Microsoft.GitHub.Signals;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.AI.Tasks;

[Neuron("tasks.task", State = "task")]
public sealed class TaskNeuron : Neuron<TaskState>, ITaskNeuron, Orleans.IRemindable
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
                IdempotencyKey = def.IdempotencyKey,
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
            ParentId = request.ParentId,
            Links = request.Links?.Select(l => new TaskLinkState { TargetId = l.TargetId, Kind = l.Kind }).ToArray() ?? [],
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
            IdempotencyKey = s.IdempotencyKey,
        }).ToArray();

        var targetStep = nextSteps[stepIndex];

        var isConfirmationGate = string.Equals(targetStep.Capability, "github.merge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(targetStep.Capability, "cluster.rollout", StringComparison.OrdinalIgnoreCase)
            || targetStep.Title.Contains("[Confirm]", StringComparison.OrdinalIgnoreCase);

        if (isConfirmationGate && targetStep.Status == TaskStepStatus.AwaitingConfirmation && newStatus != TaskStepStatus.Failed)
        {
            throw new InvalidOperationException($"Step {stepIndex} is awaiting human confirmation and must be confirmed via ConfirmStepAsync before advancing.");
        }
        if (isConfirmationGate && targetStep.Status == TaskStepStatus.Pending && newStatus == TaskStepStatus.Completed)
        {
            throw new InvalidOperationException($"Step {stepIndex} is a confirmation gate and cannot jump directly from Pending to Completed.");
        }

        if (newStatus == TaskStepStatus.Running && isConfirmationGate && targetStep.Status != TaskStepStatus.Running)
        {
            newStatus = TaskStepStatus.AwaitingConfirmation;
        }

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
        else if (newStatus == TaskStepStatus.AwaitingConfirmation)
        {
            nextTaskStatus = TaskStatus.AwaitingConfirmation;
        }
        else if (nextSteps.Length > 0 && nextSteps.All(s => s.Status == TaskStepStatus.Completed))
        {
            nextTaskStatus = TaskStatus.Completed;
            completedAt = now;
        }
        else if ((newStatus == TaskStepStatus.Running || newStatus == TaskStepStatus.Completed)
                 && (nextTaskStatus == TaskStatus.Pending || nextTaskStatus == TaskStatus.RolledBack || nextTaskStatus == TaskStatus.AwaitingConfirmation))
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
            ParentId = State.ParentId,
            Links = State.Links,
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

        if (newStatus == TaskStepStatus.Running)
        {
            try
            {
                await Context.Reminders.Start("step-redrive", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }
            catch (Exception error)
            {
                NullLogger.Instance.LogSwallowed(error, "Start reminder on step advance");
            }
        }
        else if (nextTaskStatus is TaskStatus.Completed or TaskStatus.Failed)
        {
            try
            {
                await Context.Reminders.Stop("step-redrive");
            }
            catch (Exception error)
            {
                NullLogger.Instance.LogSwallowed(error, "Stop reminder on task end");
            }
        }

        return next.ToModel();
    }

    public async Task<AgentTask> ConfirmStepAsync(int stepIndex, string? confirmationToken = null)
    {
        if (stepIndex < 0 || stepIndex >= State.Steps.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(stepIndex), $"Step {stepIndex} is out of bounds (0-{State.Steps.Length - 1}).");
        }

        var target = State.Steps[stepIndex];
        if (target.Status != TaskStepStatus.AwaitingConfirmation || State.Status != TaskStatus.AwaitingConfirmation)
        {
            throw new InvalidOperationException($"Step {stepIndex} is not awaiting confirmation (step status: {target.Status}, task status: {State.Status}).");
        }

        var now = Context.Time.GetUtcNow();
        var nextSteps = State.Steps.Select(s => new TaskStepState
        {
            Index = s.Index,
            Title = s.Title,
            Description = s.Description,
            Capability = s.Capability,
            Status = s.Index == stepIndex ? TaskStepStatus.Running : s.Status,
            RollbackCapability = s.RollbackCapability,
            Output = s.Output,
            StartedAt = s.Index == stepIndex ? (s.StartedAt ?? now) : s.StartedAt,
            CompletedAt = s.CompletedAt,
            IdempotencyKey = s.IdempotencyKey,
        }).ToArray();

        var next = new TaskState
        {
            TaskId = State.TaskId,
            Title = State.Title,
            Description = State.Description,
            Owner = State.Owner,
            Status = TaskStatus.Running,
            Steps = nextSteps,
            CreatedAt = State.CreatedAt,
            CompletedAt = State.CompletedAt,
            RollbackReason = State.RollbackReason,
            ParentId = State.ParentId,
            Links = State.Links,
        };

        var signals = new List<Signal>
        {
            new TaskStepAdvanced(next.TaskId, stepIndex, TaskStepStatus.Running)
        };

        await Save(next, signals.ToArray());

        try
        {
            await Directory.UpdateTaskStatusAsync(next.TaskId, next.Status, next.CompletedAt);
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory update on confirm");
        }

        try
        {
            await Context.Reminders.Start("step-redrive", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Start reminder on confirm");
        }

        return next.ToModel();
    }

    protected override async Task OnReminder(string name, CancellationToken cancellationToken)
    {
        if (name == "step-redrive")
        {
            await ReDriveAsync();
        }
    }

    Task Orleans.IRemindable.ReceiveReminder(string reminderName, Orleans.Runtime.TickStatus status)
        => OnReminder(reminderName, CancellationToken.None);

    internal async Task ReDriveAsync()
    {
        if (State.Status != TaskStatus.Running)
        {
            return;
        }

        var runningStep = State.Steps.FirstOrDefault(s => s.Status == TaskStepStatus.Running);
        if (runningStep is null)
        {
            return;
        }

        var signal = new TaskStepAdvanced(State.TaskId, runningStep.Index, TaskStepStatus.Running);
        if (runningStep.IdempotencyKey is null)
        {
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
                IdempotencyKey = s.Index == runningStep.Index ? $"step-{runningStep.Index}-{Guid.NewGuid():N}" : s.IdempotencyKey,
            }).ToArray();

            var next = new TaskState
            {
                TaskId = State.TaskId,
                Title = State.Title,
                Description = State.Description,
                Owner = State.Owner,
                Status = State.Status,
                Steps = nextSteps,
                CreatedAt = State.CreatedAt,
                CompletedAt = State.CompletedAt,
                RollbackReason = State.RollbackReason,
            };

            await Save(next, signal);
        }
        else
        {
            await Publish(signal);
        }
    }

    [On]
    public async Task OnPullRequestChanged(PullRequestChanged pr)
    {
        ArgumentNullException.ThrowIfNull(pr);

        if (State.Status is TaskStatus.Completed or TaskStatus.Cancelled or TaskStatus.Failed)
        {
            return;
        }

        var isFailure = string.Equals(pr.CiStatus, "failure", StringComparison.OrdinalIgnoreCase);
        var hasReviewComment = !string.IsNullOrWhiteSpace(pr.CommentAuthor) && !BotIdentity.IsBot(pr.CommentAuthor);

        if (!isFailure && !hasReviewComment)
        {
            return;
        }

        var now = Context.Time.GetUtcNow();
        var editStepIndex = Array.FindIndex(State.Steps, s => s.Capability == "microsoft.roslyn.edit");
        if (editStepIndex < 0)
        {
            editStepIndex = 1 < State.Steps.Length ? 1 : 0;
        }

        var repairIndex = State.Steps.Length;
        var description = isFailure
            ? "Repair CI failure"
            : $"Repair review comment from {pr.CommentAuthor}: {pr.CommentBody}";

        var repairStep = new TaskStepState
        {
            Index = repairIndex,
            Title = "Repair",
            Description = description,
            Capability = "microsoft.roslyn.edit",
            Status = TaskStepStatus.Pending,
            IdempotencyKey = $"repair-{repairIndex}-{Guid.NewGuid():N}",
        };

        var nextSteps = State.Steps.Select(s => new TaskStepState
        {
            Index = s.Index,
            Title = s.Title,
            Description = s.Description,
            Capability = s.Capability,
            Status = s.Index == editStepIndex ? TaskStepStatus.Running : (s.Index > editStepIndex ? TaskStepStatus.Pending : s.Status),
            RollbackCapability = s.RollbackCapability,
            Output = s.Output,
            StartedAt = s.Index == editStepIndex ? now : s.StartedAt,
            CompletedAt = s.Index >= editStepIndex ? null : s.CompletedAt,
            IdempotencyKey = s.IdempotencyKey,
        }).Concat([repairStep]).ToArray();

        var next = new TaskState
        {
            TaskId = State.TaskId,
            Title = State.Title,
            Description = State.Description,
            Owner = State.Owner,
            Status = TaskStatus.Running,
            Steps = nextSteps,
            CreatedAt = State.CreatedAt,
            CompletedAt = null,
            RollbackReason = isFailure ? "CI failure reported on pull request" : $"Review comment received from {pr.CommentAuthor}",
            ParentId = State.ParentId,
            Links = State.Links,
        };

        var signals = new List<Signal>
        {
            new TaskStepAdvanced(next.TaskId, editStepIndex, TaskStepStatus.Running),
            new TaskStepAdvanced(next.TaskId, repairIndex, TaskStepStatus.Pending),
        };

        await Save(next, signals.ToArray());

        try
        {
            await Directory.UpdateTaskStatusAsync(next.TaskId, next.Status, next.CompletedAt);
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory update on PR repair");
        }

        try
        {
            await Context.Reminders.Start("step-redrive", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Start reminder on PR repair");
        }
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
            IdempotencyKey = s.IdempotencyKey,
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
            ParentId = State.ParentId,
            Links = State.Links,
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
            ParentId = State.ParentId,
            Links = State.Links,
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

        try
        {
            await Context.Reminders.Stop("step-redrive");
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Stop reminder on cancel");
        }

        return next.ToModel();
    }

    public async Task<AgentTask> LinkAsync(string targetId, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        if (State.Links.Any(l => string.Equals(l.TargetId, targetId, StringComparison.Ordinal) && string.Equals(l.Kind, kind, StringComparison.OrdinalIgnoreCase)))
        {
            return State.ToModel();
        }

        var nextLinks = State.Links.Concat([new TaskLinkState { TargetId = targetId, Kind = kind }]).ToArray();
        var next = new TaskState
        {
            TaskId = State.TaskId,
            Title = State.Title,
            Description = State.Description,
            Owner = State.Owner,
            Status = State.Status,
            Steps = State.Steps,
            CreatedAt = State.CreatedAt,
            CompletedAt = State.CompletedAt,
            RollbackReason = State.RollbackReason,
            ParentId = State.ParentId,
            Links = nextLinks,
        };

        await Save(next, new TaskCreated(next.TaskId, next.Title, next.Owner));

        try
        {
            await Directory.RegisterTaskAsync(next.ToModel());
        }
        catch (Exception error)
        {
            NullLogger.Instance.LogSwallowed(error, "Directory update on link");
        }

        return next.ToModel();
    }
}
