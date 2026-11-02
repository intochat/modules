using System;
using System.Linq;
using System.Threading.Tasks;
using DigitalBrain;
using IntoChat.OS.Modules.AI.Tasks.Signals;

namespace IntoChat.OS.Modules.AI.Tasks;

[Neuron("tasks.directory", State = "tasks-dir")]
public class TaskDirectoryNeuron : Neuron<TaskDirectoryState>, ITaskDirectoryNeuron
{
    private const int MaxEntries = 1000;

    public async Task RegisterTaskAsync(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var existing = State.Entries.Where(e => !string.Equals(e.TaskId, task.TaskId, StringComparison.Ordinal)).ToList();
        var entry = new TaskDirectoryEntryState
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Owner = task.Owner,
            Status = task.Status,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt,
            StepCount = task.Steps.Length,
            CompletedStepCount = task.Steps.Count(s => s.Status == TaskStepStatus.Completed),
        };
        existing.Add(entry);

        var next = new TaskDirectoryState
        {
            Entries = existing.TakeLast(MaxEntries).ToArray(),
        };

        await Save(next, new TaskDirectoryChanged(next.Entries.Length, task.TaskId));
    }

    public async Task UpdateTaskStatusAsync(string taskId, TaskStatus status, DateTimeOffset? completedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var existing = State.Entries.Select(e =>
        {
            if (string.Equals(e.TaskId, taskId, StringComparison.Ordinal))
            {
                return new TaskDirectoryEntryState
                {
                    TaskId = e.TaskId,
                    Title = e.Title,
                    Description = e.Description,
                    Owner = e.Owner,
                    Status = status,
                    CreatedAt = e.CreatedAt,
                    CompletedAt = completedAt ?? e.CompletedAt,
                    StepCount = e.StepCount,
                    CompletedStepCount = e.CompletedStepCount,
                };
            }
            return e;
        }).ToArray();

        var next = new TaskDirectoryState
        {
            Entries = existing,
        };

        await Save(next, new TaskDirectoryChanged(next.Entries.Length, taskId));
    }

    public Task<AgentTask[]> ListTasksAsync(string? owner = null, TaskStatus? status = null)
    {
        var query = State.Entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(owner))
        {
            query = query.Where(e => string.Equals(e.Owner, owner, StringComparison.OrdinalIgnoreCase));
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var results = query.Select(e => new AgentTask(
            e.TaskId,
            e.Title,
            e.Description,
            e.Owner,
            e.Status,
            [],
            e.CreatedAt,
            e.CompletedAt)).ToArray();

        return Task.FromResult(results);
    }
}

[Obsolete("Renamed to TaskDirectoryNeuron.")]
public sealed class TaskDirectoryGrain : TaskDirectoryNeuron
{
}
