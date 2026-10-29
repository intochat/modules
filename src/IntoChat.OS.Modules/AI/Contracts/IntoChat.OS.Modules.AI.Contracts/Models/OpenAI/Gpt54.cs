using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed class Gpt54 : LLMModel<IGpt54>
{
    public override string Id => "gpt-5.4";

    public override AiProvider Provider => AiProvider.OpenAI;
}

[NeuronAlias("ai.llm.gpt54"), DefaultNeuronType("ai.llm.gpt54")]
public interface IGpt54 : ILLM;
