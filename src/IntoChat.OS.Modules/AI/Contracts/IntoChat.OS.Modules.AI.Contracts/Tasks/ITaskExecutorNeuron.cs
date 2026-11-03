using System.Threading.Tasks;
using DigitalBrain.Access;
using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Tasks;

[NeuronAlias("tasks.executor"), DefaultNeuronType("tasks.executor")]
[Shareable]
public interface ITaskExecutorNeuron
{
    Task<AgentTask?> ExecuteNextStepAsync(string taskId);
}
