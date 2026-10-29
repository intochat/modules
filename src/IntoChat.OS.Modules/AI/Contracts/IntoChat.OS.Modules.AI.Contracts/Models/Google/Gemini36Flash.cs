using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Models.Google;

public sealed class Gemini36Flash : LLMModel<IGemini36Flash>
{
    public override string Id => "gemini-3.6-flash";

    public override AiProvider Provider => AiProvider.Google;
}

[NeuronAlias("ai.llm.gemini36flash"), DefaultNeuronType("ai.llm.gemini36flash")]
public interface IGemini36Flash : ILLM;
