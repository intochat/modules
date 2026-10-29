using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Anthropic;

public sealed class Haiku45 : LLMModel<IHaiku45>
{
    public override string Id => "claude-haiku-4-5";

    public override AiProvider Provider => AiProvider.Anthropic;
}

[NeuronAlias("ai.llm.haiku45"), DefaultNeuronType("ai.llm.haiku45")]
public interface IHaiku45 : ILLM;
