using DigitalBrain;


namespace IntoChat.OS.Modules.AI.Models.Google;

[Neuron("ai.llm.gemini31pro")]
internal sealed class Gemini31ProNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.Google.IGemini31Pro)), IntoChat.OS.Modules.AI.Models.Google.IGemini31Pro;

[Neuron("ai.llm.gemini36flash")]
internal sealed class Gemini36FlashNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.Google.IGemini36Flash)), IntoChat.OS.Modules.AI.Models.Google.IGemini36Flash;

[Neuron("ai.llm.gemini36pro")]
internal sealed class Gemini36ProNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.Google.IGemini36Pro)), IntoChat.OS.Modules.AI.Models.Google.IGemini36Pro;
