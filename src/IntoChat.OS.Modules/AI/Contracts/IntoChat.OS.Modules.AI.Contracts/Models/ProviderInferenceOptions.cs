using System.Text.Json.Serialization;

namespace IntoChat.OS.Modules.AI.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$provider")]
[JsonDerivedType(typeof(OpenAI.OpenAIInferenceOptions), "openai")]
[JsonDerivedType(typeof(Ollama.OllamaInferenceOptions), "ollama")]
public abstract record ProviderInferenceOptions;
