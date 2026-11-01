using System;
using System.Threading.Tasks;

namespace IntoChat.OS.Modules.AI.Tasks;

[DigitalBrain.Contracts.NeuronAlias("tasks.directory"), DigitalBrain.Contracts.DefaultNeuronType("tasks.directory")]
public interface ITaskDirectoryNeuron
{
    Task RegisterTaskAsync(AgentTask task);
    Task UpdateTaskStatusAsync(string taskId, TaskStatus status, DateTimeOffset? completedAt = null);
    Task<AgentTask[]> ListTasksAsync(string? owner = null, TaskStatus? status = null);
}
