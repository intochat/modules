using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IntoChat.OS.Modules.AI.Tasks;

public interface ITaskEngine
{
    Task<AgentTask> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentTask>> ListTasksAsync(string? owner = null, TaskStatus? status = null, CancellationToken ct = default);
    Task<AgentTask> AdvanceTaskStepAsync(string taskId, int stepIndex, TaskStepStatus newStatus, string? output = null, CancellationToken ct = default);
    Task<AgentTask> RollbackTaskAsync(string taskId, int toStepIndex, string reason, CancellationToken ct = default);
    Task<AgentTask> CancelTaskAsync(string taskId, string reason, CancellationToken ct = default);
    Task<TaskVerificationResult> VerifyTaskStepAsync(string taskId, int stepIndex, CancellationToken ct = default);
}
