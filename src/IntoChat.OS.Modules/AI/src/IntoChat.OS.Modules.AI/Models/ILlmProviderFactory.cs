using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace IntoChat.OS.Modules.AI.Models;

internal interface ILlmProviderFactory
{
    AiProvider Provider { get; }

    Task<bool> IsConfigured(AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken = default);

    Task<IChatClient> CreateChatClient(LLMModel model, AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken = default);

    // The endpoint pins one client to a resolved model's endpoint; without it a hosted provider uses its registration.
    Task<IChatClient> CreateChatClient(string model, AIOptions configuration, IAiCredentials credentials, string? endpoint = null, CancellationToken cancellationToken = default);

    Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGenerator(
        EmbeddingModel model,
        AIOptions configuration,
        IAiCredentials credentials, CancellationToken cancellationToken = default);
}
