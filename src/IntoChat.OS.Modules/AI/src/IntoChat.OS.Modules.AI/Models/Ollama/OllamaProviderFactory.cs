using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace IntoChat.OS.Modules.AI.Models.Ollama;

internal sealed class OllamaProviderFactory : ILlmProviderFactory
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    public AiProvider Provider => AiProvider.Ollama;

    public Task<bool> IsConfigured(AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrEmpty(configuration.Ollama.Endpoint));

    public Task<IChatClient> CreateChatClient(LLMModel model, AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken = default)
        => CreateChatClient(configuration.Ollama.Models.GetValueOrDefault(model.Marker.Name)?.Model ?? model.Id, configuration, credentials, cancellationToken: cancellationToken);

    public Task<IChatClient> CreateChatClient(string model, AIOptions configuration, IAiCredentials credentials, string? endpoint = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IChatClient>(new ChatClientBuilder(CreateApiClient(configuration, model, model, "WithLlm", useMarkerOverride: false))
            .ConfigureOptions(static options =>
            {
                options.AdditionalProperties ??= [];
                options.AdditionalProperties.TryAdd("think", false);
            })
            .Build());

    public Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGenerator(
        EmbeddingModel model,
        AIOptions configuration,
        IAiCredentials credentials, CancellationToken cancellationToken = default)
        => Task.FromResult<IEmbeddingGenerator<string, Embedding<float>>>(CreateApiClient(configuration, model.Marker.Name, model.Id, "WithEmbedding"));

    private static string EndpointConfigurationKey => $"{AIClients.ConfigurationRoot}:Ollama:Endpoint";

    private static OllamaApiClient CreateApiClient(
        AIOptions configuration,
        string marker,
        string defaultTag,
        string hostingMethod,
        bool useMarkerOverride = true)
    {
        var tag = useMarkerOverride ? configuration.Ollama.Models.GetValueOrDefault(marker)?.Model ?? defaultTag : defaultTag;
        var http = new HttpClient
        {
            BaseAddress = RequireEndpoint(configuration, marker, hostingMethod),
            Timeout = RequestTimeout,
        };

        return new OllamaApiClient(http, tag);
    }

    private static Uri RequireEndpoint(AIOptions configuration, string marker, string hostingMethod)
    {
        var endpoint = configuration.Ollama.Endpoint;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            && (string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return endpointUri;
        }

        throw new InvalidOperationException(
            $"{marker} requires {EndpointConfigurationKey} to be an absolute HTTP(S) URI. "
            + $"Configure it through AIModule.{hostingMethod} in AppHost.");
    }
}
