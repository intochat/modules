using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.OpenRouter;

public sealed class DeepSeekV41Flash : LLMModel<IDeepSeekV41Flash>
{
    public override string Id => "deepseek/deepseek-v4.1-flash";

    public override AiProvider Provider => AiProvider.OpenRouter;
}

[NeuronAlias("ai.llm.deepseek-v41-flash"), DefaultNeuronType("ai.llm.deepseek-v41-flash")]
public interface IDeepSeekV41Flash : ILLM;
