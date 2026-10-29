using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Anthropic;

public sealed class Opus5 : LLMModel<IOpus5>
{
    public override string Id => "claude-opus-5";

    public override AiProvider Provider => AiProvider.Anthropic;
}

[NeuronAlias("ai.llm.opus5"), DefaultNeuronType("ai.llm.opus5")]
public interface IOpus5 : ILLM;
