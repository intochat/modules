namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed record OpenAIInferenceOptions(bool? ParallelToolCalls = null) : ProviderInferenceOptions;
