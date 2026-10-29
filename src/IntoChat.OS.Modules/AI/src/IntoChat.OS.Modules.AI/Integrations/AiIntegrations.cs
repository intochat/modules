using IntoChat.OS.Connections.Platform.Entries;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Framework.Modules.Platform.Integrations;

namespace IntoChat.OS.Modules.AI;

internal static class AiIntegrations
{
    internal const string ApiKeyField = "ApiKey";
    internal const string EndpointField = "Endpoint";
    internal const string TavilyId = "tavily";

    internal static IntegrationSchema OpenAI { get; } = Provider("openai", "OpenAI");
    internal static IntegrationSchema OpenRouter { get; } = Provider("openrouter", "OpenRouter");

    internal static IntegrationSchema[] Definitions { get; } =
    [
        OpenAI,
        Provider("anthropic", "Anthropic"),
        Provider("google", "Google"),
        Provider("xai", "xAI"),
        OpenRouter,
        IntegrationSchema.For(TavilyId, "Tavily").RequiresSecret(ApiKeyField),
    ];

    // The System One (Jev) integration is not a chat provider and ships no model connectors;
    // it is exposed alongside the chat integrations but kept out of the connector derivation.
    internal static IntegrationSchema TypeSafe { get; } = Provider("typesafe", "TypeSafe Jev");

    internal static IntegrationSchema[] All { get; } = [.. Definitions, TypeSafe];

    internal static string IdOf(AiProvider provider) => provider switch
    {
        AiProvider.OpenAI => "openai",
        AiProvider.Anthropic => "anthropic",
        AiProvider.Google => "google",
        AiProvider.XAI => "xai",
        AiProvider.OpenRouter => "openrouter",
        _ => throw new NotSupportedException($"{provider} is not a key-based provider."),
    };

    internal static string DefaultEndpointOf(AiProvider provider) => provider switch
    {
        AiProvider.OpenAI => "https://api.openai.com/v1",
        AiProvider.Anthropic => "https://api.anthropic.com",
        AiProvider.Google => "https://generativelanguage.googleapis.com/v1beta/openai/",
        AiProvider.XAI => "https://api.x.ai/v1",
        AiProvider.OpenRouter => "https://openrouter.ai/api/v1",
        _ => throw new NotSupportedException($"{provider} has no hosted endpoint."),
    };

    // Derived from the one list Authority's key release honours, so the two cannot drift: an id with no
    // provider here fails the type's initialisation (and every test), never a Release at runtime.
    internal static AiProvider[] KeyedProviders { get; } = [.. BrainEntries.KeyedProviderIds.Select(id => id switch
    {
        "openai" => AiProvider.OpenAI,
        "anthropic" => AiProvider.Anthropic,
        "google" => AiProvider.Google,
        "xai" => AiProvider.XAI,
        "openrouter" => AiProvider.OpenRouter,
        _ => throw new NotSupportedException($"'{id}' has no AI provider."),
    })];

    private static IntegrationSchema Provider(string id, string displayName)
        => IntegrationSchema.For(id, displayName).RequiresSecret(ApiKeyField).OffersSetting(EndpointField);
}
