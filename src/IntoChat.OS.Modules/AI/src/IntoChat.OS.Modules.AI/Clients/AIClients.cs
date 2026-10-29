using IntoChat.OS;
using IntoChat.OS.Modules.AI.Media;
using IntoChat.OS.Modules.AI.Metering;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Modules.AI.Models.Anthropic;
using IntoChat.OS.Modules.AI.Models.Google;
using IntoChat.OS.Modules.AI.Models.Ollama;
using IntoChat.OS.Modules.AI.Models.OpenAI;
using IntoChat.OS.Modules.AI.Models.OpenRouter;
using IntoChat.OS.Modules.AI.Models.XAI;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace IntoChat.OS.Modules.AI;

internal static class AIClients
{
    internal static readonly string ConfigurationRoot = AIKeys.Options;
    internal static readonly string DefaultModelKey = AIKeys.DefaultModel;
    internal static readonly string DefaultEmbeddingKey = AIKeys.DefaultEmbedding;
    internal static readonly string DefaultTranscriptionKey = AIKeys.DefaultTranscription;
    internal static readonly string DefaultImageKey = AIKeys.DefaultImage;
    private const string TelemetrySource = "IntoChat.OS.Modules.AI";

    private static readonly IReadOnlyDictionary<AiProvider, ILlmProviderFactory> Factories =
        new ILlmProviderFactory[]
        {
            new OpenAIProviderFactory(),
            new AnthropicProviderFactory(),
            new GoogleProviderFactory(),
            new XAIProviderFactory(),
            new OpenRouterProviderFactory(),
            new OllamaProviderFactory(),
        }.ToDictionary(static factory => factory.Provider);

    internal static void Add(IServiceCollection services)
    {
        services.TryAddSingleton<ProviderExecutionGate>();
        foreach (var model in LLMModel.All)
        {
            services.AddKeyedSingleton<IChatClient>(
                model.Marker,
                (provider, _) => BuildChatPipeline(provider, model));
        }

        foreach (var model in EmbeddingModel.All)
        {
            services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
                model.Marker,
                (provider, _) =>
                {
                    var inner = new RebuildingEmbeddingGenerator(
                        ct => Factories[model.Provider].CreateEmbeddingGenerator(
                            model, provider.GetRequiredService<IOptions<AIOptions>>().Value, provider.GetRequiredService<IAiCredentials>(), cancellationToken: ct),
                        GenerationOf(provider, model.Provider));
                    IEmbeddingGenerator<string, Embedding<float>> limited = new LimitedEmbeddingGenerator(inner, provider.GetRequiredService<ProviderExecutionGate>());
                    return provider.GetService<ICostMeter>() is { } meter
                        ? new MeteringEmbeddingGenerator(limited, meter, FundingOf(model.Provider), model.Id, provider.GetService<ILoggerFactory>()?.CreateLogger<MeteringEmbeddingGenerator>())
                        : limited;
                });
        }

        // Resolve the final options after application Configure delegates have run.
        // An empty marker preserves Providers' fallback to the default IChatClient.
        services.TryAddSingleton(provider => new AIDefaults(
            provider.GetRequiredService<IOptions<AIOptions>>().Value.Default.Model ?? string.Empty));
        services.TryAddSingleton(DefaultChatClient);
        services.TryAddSingleton(DefaultEmbeddingGenerator);

        services.AddHostedService<LlmWarmupHostedService>();
    }

    internal static ILlmProviderFactory Factory(AiProvider provider)
        => Factories.GetValueOrDefault(provider)
            ?? throw new ArgumentException($"Provider '{provider}' does not support runtime chat model selection.", nameof(provider));

    // What pays for a model call: a deployment integration is billed through the ledger, and a
    // local endpoint (Ollama, Foundry) runs on the host. No AI client is built from a user
    // connection today, so its funding case is never produced here.
    private static Funding FundingOf(AiProvider provider)
        => AiIntegrations.KeyedProviders.Contains(provider) ? Funding.Integration : Funding.Local;

    private static Funding FundingOf(string provider)
        => Enum.TryParse<AiProvider>(provider, out var parsed) ? FundingOf(parsed) : Funding.Local;

    private static IChatClient BuildChatPipeline(IServiceProvider provider, LLMModel model)
        => BuildChatPipeline(provider, model, Rebuilding(provider, model.Provider,
            ct => Factories[model.Provider].CreateChatClient(
                model, provider.GetRequiredService<IOptions<AIOptions>>().Value, provider.GetRequiredService<IAiCredentials>(), cancellationToken: ct)));

    // A keyed provider's client is rebuilt when its registration is re-read, so a cleared or rotated key takes effect without a restart.
    private static IChatClient Rebuilding(IServiceProvider services, AiProvider provider, Func<CancellationToken, Task<IChatClient>> create)
        => new RebuildingChatClient(create, GenerationOf(services, provider));

    private static Func<CancellationToken, Task<long>> GenerationOf(IServiceProvider services, AiProvider provider)
        => ct => AiIntegrations.KeyedProviders.Contains(provider)
            ? services.GetRequiredService<IAiCredentials>().GenerationOf(AiIntegrations.IdOf(provider), ct) : Task.FromResult(0L);

    internal static IChatClient BuildChatPipeline(IServiceProvider provider, LLMModel model, IChatClient innerClient)
        => BuildChatPipeline(provider, model.SupportsTools, model.Marker.Name, innerClient,
            model.Provider.ToString(), model.Id);

    internal static IChatClient BuildChatPipeline(IServiceProvider provider, bool supportsTools, string telemetryName,
        IChatClient innerClient, string? meterProvider = null, string? meterModel = null,
        bool rejectUnsupportedTools = false, bool useFunctionInvocation = true)
    {
        var configuration = provider.GetRequiredService<IOptions<AIOptions>>().Value;
        var loggerFactory = provider.GetService<ILoggerFactory>();
        var pipeline = new ChatClientBuilder(innerClient);
        if (!supportsTools)
        {
            // Models that cannot emit tool calls must never be told about tools —
            // the assistant then answers capability questions honestly with "no".
            pipeline = pipeline.Use(async (messages, options, next, cancellationToken) =>
            {
                if (options?.Tools is { Count: > 0 })
                {
                    if (rejectUnsupportedTools) { throw new InvalidOperationException("The pinned model does not declare tool support."); }
                    options = options.Clone();
                    options.Tools = null;
                    options.ToolMode = null;
                }

                await next(messages, options, cancellationToken).ConfigureAwait(false);
            });
        }
        // Inference-only clients leave the tool loop to the caller but still need GenAI spans.
        if (useFunctionInvocation) { pipeline = pipeline.UseFunctionInvocation(); }
        pipeline = pipeline.UseOpenTelemetry(
            loggerFactory: loggerFactory,
            sourceName: $"{TelemetrySource}.{telemetryName}",
            configure: telemetry => telemetry.EnableSensitiveData = configuration.Telemetry.EnableSensitiveData ?? false);
        // Innermost: one metered entry per raw provider call, before any function-invocation
        // aggregation, and never a second entry for the same call.
        if (meterProvider is not null && meterModel is not null && provider.GetService<ICostMeter>() is { } meter)
        {
            pipeline = pipeline.Use(inner => new MeteringChatClient(inner, meter, FundingOf(meterProvider), meterModel, loggerFactory?.CreateLogger<MeteringChatClient>()));
        }
        if (provider.GetService<ProviderExecutionGate>() is { } execution)
        { pipeline = pipeline.Use(inner => new LimitedChatClient(inner, execution)); }
        return pipeline.Build(provider);
    }

    private static IChatClient DefaultChatClient(IServiceProvider provider)
        => new RebuildingChatClient(ct => DefaultChatClientAsync(provider, ct), _ => Task.FromResult(0L));

    private static async Task<IChatClient> DefaultChatClientAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var configuration = provider.GetRequiredService<IOptions<AIOptions>>().Value;
        // A default that names a profile, or pins reasoning or output tokens, resolves through the catalog.
        // This client runs the tools it is offered (PlaywrightWebAgent relies on that), so it keeps
        // function invocation and rejects tools its model does not declare.
        var defaultModel = configuration.Default.Model;
        if ((defaultModel is { Length: > 0 } && configuration.ModelProfiles.ContainsKey(defaultModel))
            || configuration.Default.Reasoning is not null || configuration.Default.MaxOutputTokens is not null)
        {
            var catalog = provider.GetRequiredService<ModelCatalog>();
            var resolved = await catalog.Resolve(null, requiresTools: false, cancellationToken);
            if (resolved.Provider is AiProvider.Host) { throw new InvalidOperationException("No chat provider is configured. Provision a provider integration or configure an Ollama endpoint."); }
            return await catalog.CreateClient(resolved, cancellationToken, invokesTools: true);
        }
        var model = configuration.Default.Model is { Length: > 0 } markerName
            ? LLMModel.FindByMarkerName(markerName)
                ?? throw UnknownMarker(DefaultModelKey, markerName, LLMModel.All.Select(static m => m.Marker.Name))
            : await FirstConfiguredModel(configuration, provider.GetRequiredService<IAiCredentials>(), cancellationToken);
        return provider.GetRequiredKeyedService<IChatClient>(model.Marker);
    }

    private static async Task<LLMModel> FirstConfiguredModel(AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken)
    {
        foreach (var model in LLMModel.All)
        {
            if (await Factories[model.Provider].IsConfigured(configuration, credentials, cancellationToken)) { return model; }
        }
        throw new InvalidOperationException("No LLM provider is configured. Provision a provider integration, supply an Ollama endpoint, or pin " + DefaultModelKey + ".");
    }

    private static IEmbeddingGenerator<string, Embedding<float>> DefaultEmbeddingGenerator(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IOptions<AIOptions>>().Value;

        if (configuration.Default.Embedding is { Length: > 0 } markerName)
        {
            var configured = EmbeddingModel.FindByMarkerName(markerName)
                ?? throw UnknownMarker(DefaultEmbeddingKey, markerName, EmbeddingModel.All.Select(static m => m.Marker.Name));
            return provider.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>(configured.Marker);
        }

        // The default embedding stays pinned to the local model unless explicitly
        // configured: silently switching it changes vector dimensions and orphans
        // every existing Qdrant collection.
        var local = EmbeddingModel.All.Single(static model => model.Marker == typeof(Models.Ollama.IEmbeddingGemma));
        if (string.IsNullOrEmpty(configuration.Ollama.Endpoint))
        {
            throw new InvalidOperationException(
                $"No embedding model is configured. Supply an Ollama endpoint for {local.Marker.Name}, "
                + $"or pin {DefaultEmbeddingKey} to a configured cloud embedding "
                + $"({string.Join(", ", EmbeddingModel.All.Select(static m => m.Marker.Name))}).");
        }

        return provider.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>(local.Marker);
    }

    internal static void AddImageGeneration(IServiceCollection services, IConfiguration configuration)
        => AddImageGeneration(services, AIOptionsRuntime.Read(configuration));

    internal static void AddImageGeneration(IServiceCollection services, AIOptions configuration)
    {
        // The marker names the model, as with chat and embeddings; an unpinned
        // key keeps the catalogue's first entry.
        var markerName = configuration.Default.Image;
        var model = string.IsNullOrEmpty(markerName)
            ? ImageModel.All[0]
            : ImageModel.FindByMarkerName(markerName);

        // Resolution failure is deferred into the factory rather than thrown here:
        // this runs during service registration, so throwing would take the whole
        // silo down over a typo in a peripheral feature. Default:Model does the
        // same, and Default:Transcription degrades to a 503 with the reason.
        if (model is null)
        {
            services.AddSingleton<IImageGeneration>(_ => throw UnknownMarker(
                DefaultImageKey, markerName!, ImageModel.All.Select(static m => m.Marker.Name)));
            return;
        }

        services.AddSingleton<IImageGeneration>(sp =>
            new OpenAIImageGeneration(model, sp.GetRequiredService<IAiCredentials>()));
    }

    private static InvalidOperationException UnknownMarker(
        string configurationKey,
        string markerName,
        IEnumerable<string> knownMarkerNames)
        => new($"{configurationKey} names unknown model '{markerName}'. "
            + $"Known models: {string.Join(", ", knownMarkerNames)}.");
}
