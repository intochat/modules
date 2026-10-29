using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed class Gpt54Mini : LLMModel<IGpt54Mini>
{
    public override string Id => "gpt-5.4-mini";

    public override AiProvider Provider => AiProvider.OpenAI;
}

[NeuronAlias("ai.llm.gpt54mini"), DefaultNeuronType("ai.llm.gpt54mini")]
public interface IGpt54Mini : ILLM;
