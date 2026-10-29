using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Google;

public sealed class Gemini31Pro : LLMModel<IGemini31Pro>
{
    public override string Id => "gemini-3.1-pro-preview";

    public override AiProvider Provider => AiProvider.Google;
}

[NeuronAlias("ai.llm.gemini31pro"), DefaultNeuronType("ai.llm.gemini31pro")]
public interface IGemini31Pro : ILLM;
