using DigitalBrain;


namespace IntoChat.OS.Modules.AI.Models.OpenRouter;

[Neuron("ai.llm.deepseek-v41-flash")]
internal sealed class DeepSeekV41FlashNeuron(IModelInference inference)
    : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenRouter.IDeepSeekV41Flash)), IntoChat.OS.Modules.AI.Models.OpenRouter.IDeepSeekV41Flash;
