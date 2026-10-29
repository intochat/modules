namespace IntoChat.OS.Modules.AI.Models.Ollama;

public sealed record OllamaInferenceOptions(
    int? ContextWindowTokens = null,
    bool? Think = null) : ProviderInferenceOptions;
