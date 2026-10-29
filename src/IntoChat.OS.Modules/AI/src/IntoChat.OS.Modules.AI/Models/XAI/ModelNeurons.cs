using DigitalBrain;


namespace IntoChat.OS.Modules.AI.Models.XAI;

[Neuron("ai.llm.grok46")]
internal sealed class Grok46Neuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.XAI.IGrok46)), IntoChat.OS.Modules.AI.Models.XAI.IGrok46;
