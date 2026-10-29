using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed class Gpt54Nano : LLMModel<IGpt54Nano>
{
    public override string Id => "gpt-5.4-nano";

    public override AiProvider Provider => AiProvider.OpenAI;
}

[NeuronAlias("ai.llm.gpt54nano"), DefaultNeuronType("ai.llm.gpt54nano")]
public interface IGpt54Nano : ILLM;
