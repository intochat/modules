using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Anthropic;

public sealed class Fable5 : LLMModel<IFable5>
{
    public override string Id => "claude-fable-5";

    public override AiProvider Provider => AiProvider.Anthropic;
}

[NeuronAlias("ai.llm.fable5"), DefaultNeuronType("ai.llm.fable5")]
public interface IFable5 : ILLM;
