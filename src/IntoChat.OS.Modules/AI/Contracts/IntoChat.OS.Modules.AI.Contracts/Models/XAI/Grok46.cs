using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.XAI;

public sealed class Grok46 : LLMModel<IGrok46>
{
    public override string Id => "grok-4.6";

    public override AiProvider Provider => AiProvider.XAI;
}

[NeuronAlias("ai.llm.grok46"), DefaultNeuronType("ai.llm.grok46")]
public interface IGrok46 : ILLM;
