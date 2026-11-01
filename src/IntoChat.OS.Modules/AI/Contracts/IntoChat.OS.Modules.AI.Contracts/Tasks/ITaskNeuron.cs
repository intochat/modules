using System.Threading.Tasks;

namespace IntoChat.OS.Modules.AI.Tasks;

[DigitalBrain.Contracts.NeuronAlias("tasks.task"), DigitalBrain.Contracts.DefaultNeuronType("tasks.task")]
public interface ITaskNeuron
{
    Task<AgentTask> InitializeAsync(CreateTaskRequest request);
    Task<AgentTask> GetAsync();
    Task<AgentTask> AdvanceStepAsync(int stepIndex, TaskStepStatus newStatus, string? output = null);
    Task<AgentTask> RollbackAsync(int toStepIndex, string reason);
    Task<AgentTask> CancelAsync(string reason);
    Task<TaskVerificationResult> VerifyStepAsync(int stepIndex);
}
