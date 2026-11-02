using System.Threading.Tasks;
using DigitalBrain.Access;

namespace IntoChat.OS.Modules.AI.Tasks;

[DigitalBrain.Contracts.NeuronAlias("tasks.task"), DigitalBrain.Contracts.DefaultNeuronType("tasks.task")]
[Shareable]
public interface ITaskNeuron
{
    Task<AgentTask> InitializeAsync(CreateTaskRequest request);
    Task<AgentTask> GetAsync();
    Task<AgentTask> AdvanceStepAsync(int stepIndex, TaskStepStatus newStatus, string? output = null);
    Task<AgentTask> RollbackAsync(int toStepIndex, string reason);
    Task<AgentTask> CancelAsync(string reason);
    Task<TaskVerificationResult> VerifyStepAsync(int stepIndex);
    Task<AgentTask> ConfirmStepAsync(int stepIndex, string? confirmationToken = null);
}
