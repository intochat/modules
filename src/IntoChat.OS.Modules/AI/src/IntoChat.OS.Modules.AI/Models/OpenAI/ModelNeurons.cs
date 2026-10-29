using DigitalBrain;


namespace IntoChat.OS.Modules.AI.Models.OpenAI;

[Neuron("ai.llm.gpt54")]
internal sealed class Gpt54Neuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenAI.IGpt54)), IntoChat.OS.Modules.AI.Models.OpenAI.IGpt54;

[Neuron("ai.llm.gpt54mini")]
internal sealed class Gpt54MiniNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenAI.IGpt54Mini)), IntoChat.OS.Modules.AI.Models.OpenAI.IGpt54Mini;

[Neuron("ai.llm.gpt54nano")]
internal sealed class Gpt54NanoNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenAI.IGpt54Nano)), IntoChat.OS.Modules.AI.Models.OpenAI.IGpt54Nano;

[Neuron("ai.llm.gpt56luna")]
internal sealed class Gpt56LunaNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenAI.IGpt56Luna)), IntoChat.OS.Modules.AI.Models.OpenAI.IGpt56Luna;

[Neuron("ai.llm.gpt56sol")]
internal sealed class Gpt56SolNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenAI.IGpt56Sol)), IntoChat.OS.Modules.AI.Models.OpenAI.IGpt56Sol;

[Neuron("ai.llm.gpt56terra")]
internal sealed class Gpt56TerraNeuron(IModelInference inference) : LlmNeuronBase(inference, typeof(IntoChat.OS.Modules.AI.Models.OpenAI.IGpt56Terra)), IntoChat.OS.Modules.AI.Models.OpenAI.IGpt56Terra;
