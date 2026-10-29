using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed class Gpt56Terra : LLMModel<IGpt56Terra>
{
    public override string Id => "gpt-5.6-terra";

    public override AiProvider Provider => AiProvider.OpenAI;
}

[NeuronAlias("ai.llm.gpt56terra"), DefaultNeuronType("ai.llm.gpt56terra")]
public interface IGpt56Terra : ILLM;
