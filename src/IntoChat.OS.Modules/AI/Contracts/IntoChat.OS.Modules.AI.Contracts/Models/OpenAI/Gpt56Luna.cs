using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed class Gpt56Luna : LLMModel<IGpt56Luna>
{
    public override string Id => "gpt-5.6-luna";

    public override AiProvider Provider => AiProvider.OpenAI;
}

[NeuronAlias("ai.llm.gpt56luna"), DefaultNeuronType("ai.llm.gpt56luna")]
public interface IGpt56Luna : ILLM;
