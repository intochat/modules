using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Google;

public sealed class Gemini36Pro : LLMModel<IGemini36Pro>
{
    public override string Id => "gemini-3.6-pro";

    public override AiProvider Provider => AiProvider.Google;
}

[NeuronAlias("ai.llm.gemini36pro"), DefaultNeuronType("ai.llm.gemini36pro")]
public interface IGemini36Pro : ILLM;
