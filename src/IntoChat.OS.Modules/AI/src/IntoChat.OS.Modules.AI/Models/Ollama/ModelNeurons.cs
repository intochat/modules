using DigitalBrain;


namespace IntoChat.OS.Modules.AI.Models.Ollama;

[Neuron("ai.llm.gemma4")]
internal sealed class Gemma4Neuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.Ollama.IGemma4)), IntoChat.OS.Modules.AI.Models.Ollama.IGemma4;

[Neuron("ai.llm.qwen35")]
internal sealed class Qwen35Neuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.Ollama.IQwen35)), IntoChat.OS.Modules.AI.Models.Ollama.IQwen35;
