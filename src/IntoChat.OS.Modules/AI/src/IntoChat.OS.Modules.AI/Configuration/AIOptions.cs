using System.Text.Json.Serialization;
using IntoChat.OS;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.AI;

public sealed class AIOptions : IModuleOptions
{
    public AIDefaultOptions Default { get; set; } = new();
    public AITelemetryOptions Telemetry { get; set; } = new();
    public AIProviderOptions OpenAI { get; set; } = new();
    public AIProviderOptions Anthropic { get; set; } = new();
    public AIProviderOptions Google { get; set; } = new();
    public AIProviderOptions XAI { get; set; } = new();
    public AIProviderOptions OpenRouter { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();
    public TavilyOptions Tavily { get; set; } = new();
    public SystemOneOptions SystemOne { get; set; } = new();
    public AIHostingOptions Hosting { get; set; } = new();
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, AIModelProfileOptions> ModelProfiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public AIProviderOptions Provider(AiProvider provider) => provider switch
    {
        AiProvider.OpenAI => OpenAI,
        AiProvider.Anthropic => Anthropic,
        AiProvider.Google => Google,
        AiProvider.XAI => XAI,
        AiProvider.OpenRouter => OpenRouter,
        AiProvider.Ollama => Ollama,
        _ => throw new NotSupportedException($"{provider} has no hosted provider settings."),
    };

    // Options validate only their own values; marker names are the hosting ring's
    // question, resolved loudly once at bind in AIModuleHosting.
    public void Validate() { }

    // Model markers are open-ended child keys beside Endpoint, rather than under Models.
    // Keep that public configuration shape.

}
