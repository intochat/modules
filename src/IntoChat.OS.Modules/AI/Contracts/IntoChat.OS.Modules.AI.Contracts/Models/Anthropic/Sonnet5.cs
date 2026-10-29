using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Anthropic;

public sealed class Sonnet5 : LLMModel<ISonnet5>
{
    public override string Id => "claude-sonnet-5";

    public override AiProvider Provider => AiProvider.Anthropic;
}

[NeuronAlias("ai.llm.sonnet5"), DefaultNeuronType("ai.llm.sonnet5")]
public interface ISonnet5 : ILLM;
