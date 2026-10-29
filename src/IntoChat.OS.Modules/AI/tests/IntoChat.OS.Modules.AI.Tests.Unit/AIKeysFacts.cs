namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class AIKeysFacts
{
    [Fact]
    public void EveryKeyLivesUnderTheModuleOptionsSection()
    {
        string[] keys =
        [
            AIKeys.DefaultModel, AIKeys.DefaultEmbedding, AIKeys.DefaultTranscription, AIKeys.DefaultImage, AIKeys.DefaultSystemOne,
            AIKeys.Ollama, AIKeys.OllamaEndpoint, AIKeys.OllamaModels, AIKeys.TavilyEnabled, AIKeys.TelemetryEnableSensitiveData,
        ];

        Assert.False(string.IsNullOrWhiteSpace(AIKeys.Options));
        Assert.All(keys, key => Assert.StartsWith(AIKeys.Options + ":", key, StringComparison.Ordinal));
    }

    [Fact]
    public void KeysAreDistinct()
    {
        string[] keys =
        [
            AIKeys.DefaultModel, AIKeys.DefaultEmbedding, AIKeys.DefaultTranscription, AIKeys.DefaultImage, AIKeys.DefaultSystemOne,
            AIKeys.Ollama, AIKeys.OllamaEndpoint, AIKeys.OllamaModels, AIKeys.TavilyEnabled, AIKeys.TelemetryEnableSensitiveData,
        ];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheOllamaKeysNestUnderTheOllamaSection()
    {
        Assert.Equal(AIKeys.Ollama + ":Endpoint", AIKeys.OllamaEndpoint);
        Assert.Equal(AIKeys.Ollama + ":Models", AIKeys.OllamaModels);
    }

    [Fact]
    public void TheDefaultKeysNameTheirCapability()
    {
        Assert.EndsWith(":Default:Model", AIKeys.DefaultModel, StringComparison.Ordinal);
        Assert.EndsWith(":Default:Embedding", AIKeys.DefaultEmbedding, StringComparison.Ordinal);
        Assert.EndsWith(":Default:SystemOne", AIKeys.DefaultSystemOne, StringComparison.Ordinal);
    }
}
