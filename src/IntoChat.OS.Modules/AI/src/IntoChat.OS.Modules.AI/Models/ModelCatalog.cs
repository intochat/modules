using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Contracts.Compute;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.AI.Models;

// The one resolver from a model key to a client. A key is a marker name or a configured profile
// name; null is the deployment default. brainId is the calling brain: its own provider keys make
// models available to it alone and win over the deployment registration. A null brain sees only the
// deployment's registrations.
public sealed class ModelCatalog(IServiceProvider services, IOptionsMonitor<AIOptions> options)
{
    private IAiCredentials Deployment => services.GetRequiredService<IAiCredentials>();

    private IAiCredentials CredentialsFor(string? brainId)
        => brainId is not null && BrainScope.IsValidId(brainId) && services.GetService<BrainKeyAvailability>() is { } keys
            ? new BrainAwareCredentials(Deployment, keys, brainId)
            : Deployment;

    internal bool HasProfile(string name) => options.CurrentValue.ModelProfiles.ContainsKey(name);

    public Task<ResolvedModel> Resolve(ModelSelection? selection, bool requiresTools, CancellationToken ct)
        => Resolve(null, selection, requiresTools, ct);

    public async Task<ResolvedModel> Resolve(string? brainId, ModelSelection? selection, bool requiresTools, CancellationToken ct)
    {
        var credentials = CredentialsFor(brainId);
        selection ??= new();
        var configuration = options.CurrentValue;
        var useDefault = string.IsNullOrWhiteSpace(selection.Model);
        var key = Clean(selection.Model) ?? Clean(configuration.Default.Model);
        var reasoning = Clean(selection.Reasoning) ?? (useDefault ? Clean(configuration.Default.Reasoning) : null);
        var maxTokens = selection.MaxOutputTokens ?? (useDefault ? configuration.Default.MaxOutputTokens : null);

        AiProvider provider;
        string modelId;
        string? endpoint = null;
        string? profileName = null;
        LlmCapabilities? declared = null;
        LLMModel? preset = null;
        if (key is null)
        {
            // Automatic: the configured model with the lowest list input price, among tool-capable
            // models when tools are required. Ties keep catalogue order.
            var prices = services.GetService<IPriceList>();
            foreach (var candidate in LLMModel.All.Where(model => !requiresTools || model.SupportsTools).OrderBy(model => InputPrice(prices, model)))
            {
                if (await AIClients.Factory(candidate.Provider).IsConfigured(configuration, credentials, ct)) { preset = candidate; break; }
            }
            if (preset is null)
            {
                var host = new ResolvedModel(AiProvider.Host, "default", null, null, string.Empty, LlmCapabilities.Tools, reasoning, maxTokens);
                return host with { Revision = Revision(host) };
            }
            provider = preset.Provider;
            modelId = preset.Id;
        }
        else if (configuration.ModelProfiles.TryGetValue(key, out var profile))
        {
            profileName = key;
            provider = ParseProvider(Clean(profile.Provider) ?? throw new ArgumentException($"Model profile '{key}' requires a provider.", nameof(selection)));
            modelId = Clean(profile.Model) ?? throw new ArgumentException($"Model profile '{key}' requires a model ID.", nameof(selection));
            endpoint = Clean(profile.Endpoint);
            reasoning = Clean(selection.Reasoning) ?? Clean(profile.Reasoning);
            maxTokens = selection.MaxOutputTokens ?? profile.MaxOutputTokens;
            declared = profile.Capabilities;
            preset = LLMModel.FindById(modelId);
            if (preset is not null && preset.Provider != provider) { preset = null; }
        }
        else if (LLMModel.FindByMarkerName(key) is { } marker)
        {
            preset = marker;
            provider = marker.Provider;
            modelId = marker.Id;
        }
        else
        {
            throw new ArgumentException($"Unknown model '{key}'. Use a model marker name or a configured profile name.", nameof(selection));
        }

        if (provider == AiProvider.Ollama && preset is not null)
        { modelId = Clean(configuration.Ollama.Models.GetValueOrDefault(preset.Marker.Name)?.Model) ?? modelId; }
        if (modelId.Length > 200 || modelId.Any(char.IsControl))
        { throw new ArgumentException("Model ID must contain at most 200 printable characters.", nameof(selection)); }

        var factory = AIClients.Factory(provider);
        if (!await factory.IsConfigured(configuration, credentials, ct) && !(provider == AiProvider.Ollama && endpoint is not null))
        {
            if (provider == AiProvider.Ollama) { throw new InvalidOperationException($"Provider 'Ollama' is not configured. Set {AIKeys.OllamaEndpoint}."); }
            await credentials.RequireReady(AiIntegrations.IdOf(provider), ct);
        }
        // Integration endpoints and keys are materialized together when the client is used; persist
        // only explicit host/profile overrides, where null means the provider integration default.
        endpoint ??= provider == AiProvider.Ollama ? Clean(configuration.Ollama.Endpoint) : Clean(configuration.Provider(provider).Endpoint);
        if (endpoint is not null || provider == AiProvider.Ollama) { endpoint = Endpoint(endpoint, provider); }

        var capabilities = declared ?? preset?.Capabilities ?? LlmCapabilities.None;
        if ((capabilities & ~(LlmCapabilities.Tools | LlmCapabilities.Vision | LlmCapabilities.StructuredOutput)) != 0)
        { throw new ArgumentException("Model capabilities contain unknown flags.", nameof(selection)); }
        if (requiresTools && !capabilities.HasFlag(LlmCapabilities.Tools))
        { throw new ArgumentException($"Model '{modelId}' does not declare tool support. Configure Tools capabilities in its model profile before attaching tools.", nameof(selection)); }
        reasoning = NormalizeReasoning(reasoning);
        if (requiresTools && provider == AiProvider.OpenAI && modelId.StartsWith("gpt-5.6", StringComparison.OrdinalIgnoreCase))
        {
            if (reasoning is not null and not "none") { throw new ArgumentException("GPT-5.6 tools through OpenAI Chat Completions require reasoning 'none'.", nameof(selection)); }
            reasoning = "none";
        }
        if (maxTokens is < 1 or > 1_000_000) { throw new ArgumentException("MaxOutputTokens must be between 1 and 1000000.", nameof(selection)); }
        var resolved = new ResolvedModel(provider, modelId, endpoint, profileName, string.Empty, capabilities, reasoning, maxTokens);
        return resolved with { Revision = Revision(resolved) };
    }

    // By default the pipeline carries GenAI telemetry and metering but never function invocation:
    // inference returns proposed calls and the agent owns the only tool loop. invokesTools is for
    // a caller that hands the client AIFunctions and expects it to run them; it also rejects tools
    // the model does not declare. The Host client is the deployment's and is not owned by the caller.
    public Task<IChatClient> CreateClient(ResolvedModel resolved, CancellationToken ct, bool invokesTools = false)
        => CreateClient(null, resolved, ct, invokesTools);

    public async Task<IChatClient> CreateClient(string? brainId, ResolvedModel resolved, CancellationToken ct, bool invokesTools = false)
    {
        var credentials = CredentialsFor(brainId);
        ArgumentNullException.ThrowIfNull(resolved);
        if (!string.Equals(resolved.Revision, Revision(resolved), StringComparison.Ordinal))
        { throw new ArgumentException("The resolved model configuration does not match its revision.", nameof(resolved)); }
        // Pinned options are applied inside the normal pipeline. Validate them before they
        // can overwrite the per-call ceiling at the provider boundary.
        _ = services.GetService<ProviderExecutionGate>()?.Bound(new ChatOptions { MaxOutputTokens = resolved.MaxOutputTokens });
        if (resolved.Provider == AiProvider.Host) { return new NonDisposing(services.GetRequiredService<IChatClient>()); }
        var configuration = new AIOptions();
        configuration.Provider(resolved.Provider).Endpoint = resolved.Endpoint;
        var inner = new RebuildingChatClient(
            token => Build(brainId, resolved, configuration, token),
            token => AiIntegrations.KeyedProviders.Contains(resolved.Provider) ? credentials.GenerationOf(AiIntegrations.IdOf(resolved.Provider), token) : Task.FromResult(0L));
        var pinned = new ChatClientBuilder(inner).ConfigureOptions(request =>
        {
            request.ModelId = resolved.Model;
            if (resolved.Reasoning is not null) { request.Reasoning = Reasoning(resolved.Reasoning); }
            if (resolved.MaxOutputTokens is not null) { request.MaxOutputTokens = resolved.MaxOutputTokens; }
        }).Build();
        ct.ThrowIfCancellationRequested();
        return AIClients.BuildChatPipeline(services, !invokesTools || resolved.Capabilities.HasFlag(LlmCapabilities.Tools),
            resolved.Provider.ToString(), pinned, resolved.Provider.ToString(), resolved.Model,
            rejectUnsupportedTools: invokesTools, useFunctionInvocation: invokesTools);
    }

    // One release per build decides whose key this client carries. A brain key goes only to the
    // provider's public endpoint: operator-declared and registration endpoints are the deployment's and
    // never see it. An operator profile with its own endpoint is a deployment model: it always uses the
    // deployment's key and that endpoint, and the brain key is not even released for it.
    private async Task<IChatClient> Build(string? brainId, ResolvedModel resolved, AIOptions configuration, CancellationToken ct)
    {
        var factory = AIClients.Factory(resolved.Provider);
        if (brainId is not null && BrainScope.IsValidId(brainId) && AiIntegrations.KeyedProviders.Contains(resolved.Provider)
            && !HasProfileEndpoint(resolved)
            && await Deployment.BrainKey(brainId, resolved.Provider, ct) is { Length: > 0 } key)
        {
            var publicEndpoint = services.GetRequiredService<ProviderPublicEndpoints>().Of(resolved.Provider);
            return await factory.CreateChatClient(resolved.Model, new AIOptions(), new BrainKeyCredentials(AiIntegrations.IdOf(resolved.Provider), key), publicEndpoint, ct);
        }
        return await factory.CreateChatClient(resolved.Model, configuration, Deployment, resolved.Endpoint, ct);
    }

    private bool HasProfileEndpoint(ResolvedModel resolved)
        => resolved.Profile is not null
            && Clean(options.CurrentValue.ModelProfiles.GetValueOrDefault(resolved.Profile)?.Endpoint) is not null;

    public ModelDescriptor Describe(ResolvedModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var profile = model.Profile is not null ? options.CurrentValue.ModelProfiles.GetValueOrDefault(model.Profile) : null;
        if (profile?.ContextWindowTokens is <= 0 || profile?.MaximumOutputTokens is <= 0 || profile?.MaximumOutputTokens > profile?.ContextWindowTokens)
        { throw new ArgumentException("Configured model token limits must be positive and output cannot exceed context."); }
        if (profile?.AllowedReasoning?.Any(value => value is not ("none" or "low" or "medium" or "high" or "xhigh")) == true)
        { throw new ArgumentException("AllowedReasoning contains an unsupported reasoning value."); }
        var declared = profile?.Capabilities;
        CapabilitySupport Support(LlmCapabilities capability) => model.Capabilities.HasFlag(capability)
            ? CapabilitySupport.Supported : declared is null ? CapabilitySupport.Unknown : CapabilitySupport.Unsupported;
        return new(model.Provider.ToString(), model.Model, model.Profile, model.Revision,
            Support(LlmCapabilities.Tools), Support(LlmCapabilities.Vision), Support(LlmCapabilities.StructuredOutput),
            profile?.ContextWindowTokens, profile?.MaximumOutputTokens, SupportFlag(profile?.SupportsTemperature),
            SupportFlag(profile?.SupportsTopP), profile?.AllowedReasoning?.ToArray());
    }

    // The browser's allowlist: never a resolved model, which carries operator endpoints.
    // Catalogued models whose provider has no key yet are listed as unavailable, so a picker can show
    // them and say why; profiles that do not resolve are left out.
    public Task<ModelChoices> List(string? defaultModel, CancellationToken ct) => List(null, defaultModel, ct);

    public async Task<ModelChoices> List(string? brainId, string? defaultModel, CancellationToken ct)
    {
        var automatic = await TryResolve(brainId, Default(defaultModel), ct);
        var choices = new List<ModelChoice>();
        foreach (var choice in Choices())
        {
            var (resolved, reason) = await Attempt(brainId, choice.Selection, ct);
            if (resolved is not null) { choices.Add(Describe(choice.Id, choice.Label, resolved)); }
            else if (choice.Preset is { } preset && preset.SupportsTools && reason is not null)
            { choices.Add(new(choice.Id, choice.Label, preset.Provider.ToString(), preset.Id, false, [.. Flags(preset.Capabilities)]) { Reason = reason }); }
        }
        return new(Describe(null, "Automatic (workspace default)", automatic is { Provider: not AiProvider.Host } ? automatic : null), [.. choices]);
    }

    public Task<ModelSelection?> Select(string? id, string? defaultModel, CancellationToken ct) => Select(null, id, defaultModel, ct);

    public async Task<ModelSelection?> Select(string? brainId, string? id, string? defaultModel, CancellationToken ct)
    {
        var selection = id is null ? Default(defaultModel)
            : Choices().FirstOrDefault(choice => string.Equals(choice.Id, id, StringComparison.Ordinal))?.Selection
                ?? throw new ArgumentException("The selected model is unavailable. Choose a model from Settings.");
        const string Unavailable = "The selected model is unavailable or does not support assistant tools. Choose a model from Settings.";
        ResolvedModel resolved;
        try { resolved = await Resolve(brainId, selection, requiresTools: true, ct); }
        catch (ProviderUnavailableException) { throw; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
        { throw new ArgumentException(Unavailable); }
        // The host client is a deployment fallback, not a choice a user can make.
        if (resolved.Provider is AiProvider.Host) { throw new ArgumentException(Unavailable); }
        return selection;
    }

    private static ModelSelection? Default(string? defaultModel) => defaultModel is { Length: > 0 } model ? new(model) : null;

    private sealed record Choice(string Id, string Label, ModelSelection Selection, LLMModel? Preset = null);

    // Micros per million input tokens from the module's price list; a local model without a listed
    // price is free, any other unlisted model sorts after every priced one.
    private static long InputPrice(IPriceList? prices, LLMModel model)
        => prices?.MicrosPerUnit(CostKind.Model, model.Id + ":input") ?? (model.Provider == AiProvider.Ollama ? 0 : long.MaxValue);

    private static IEnumerable<string> Flags(LlmCapabilities capabilities)
        => Enum.GetValues<LlmCapabilities>().Where(c => c != LlmCapabilities.None && capabilities.HasFlag(c)).Select(c => c.ToString());

    private IEnumerable<Choice> Choices()
    {
        foreach (var name in options.CurrentValue.ModelProfiles.Keys.Order(StringComparer.OrdinalIgnoreCase))
        { yield return new("profile:" + name, name, new(name)); }
        foreach (var preset in LLMModel.All)
        {
            // A local server being configured does not imply every compiled local model is installed.
            if (preset.Provider == AiProvider.Ollama && !options.CurrentValue.Ollama.Models.ContainsKey(preset.Marker.Name)) { continue; }
            yield return new("preset:" + preset.Marker.Name, preset.Id, new(preset.Marker.Name), preset);
        }
    }

    internal const string NoKeyReason = "Add a key in API keys";

    // A catalogued model that does not resolve says why: a key-based provider without a key asks for
    // one; any other refusal carries its own message (catalog messages name no endpoint or secret).
    private async Task<(ResolvedModel? Resolved, string? Reason)> Attempt(string? brainId, ModelSelection selection, CancellationToken ct)
    {
        try { return (await Resolve(brainId, selection, requiresTools: true, ct), null); }
        catch (ProviderUnavailableException) { return (null, NoKeyReason); }
        catch (ArgumentException error) { return (null, error.Message); }
        catch (InvalidOperationException error) { return (null, error.Message); }
        catch (NotSupportedException error) { return (null, error.Message); }
    }

    private async Task<ResolvedModel?> TryResolve(string? brainId, ModelSelection? selection, CancellationToken ct)
    {
        try { return await Resolve(brainId, selection, requiresTools: true, ct); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException) { return null; }
    }

    private static ModelChoice Describe(string? id, string label, ResolvedModel? model) => new(id, label,
        model?.Provider.ToString(), model?.Model, model is not null,
        model is null ? [] : [.. Flags(model.Capabilities)]);

    private static string Revision(ResolvedModel resolved)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resolved with { Revision = string.Empty }))));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CapabilitySupport SupportFlag(bool? value)
        => value is null ? CapabilitySupport.Unknown : value.Value ? CapabilitySupport.Supported : CapabilitySupport.Unsupported;

    private static AiProvider ParseProvider(string name)
        => Enum.TryParse<AiProvider>(name, ignoreCase: true, out var provider) && Enum.IsDefined(provider)
            && provider is not (AiProvider.FoundryLocal or AiProvider.Host)
            ? provider : throw new ArgumentException($"Unknown chat provider '{name}'. Use OpenAI, Anthropic, Google, XAI, OpenRouter or Ollama.", nameof(name));

    private static string? Endpoint(string? endpoint, AiProvider provider)
    {
        endpoint ??= provider switch
        {
            AiProvider.OpenAI => "https://api.openai.com/v1",
            AiProvider.Anthropic => "https://api.anthropic.com",
            AiProvider.Google => "https://generativelanguage.googleapis.com/v1beta/openai/",
            AiProvider.XAI => "https://api.x.ai/v1",
            AiProvider.OpenRouter => "https://openrouter.ai/api/v1",
            _ => null,
        };
        if (endpoint is null || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        { throw new ArgumentException("Provider endpoints must be absolute HTTP(S) URLs without credentials, query strings or fragments.", nameof(endpoint)); }
        return uri.AbsoluteUri;
    }

    private static string? NormalizeReasoning(string? value) => value?.ToLowerInvariant() switch
    {
        null => null,
        "none" => "none",
        "low" => "low",
        "medium" => "medium",
        "high" => "high",
        "xhigh" or "extrahigh" or "extra-high" => "xhigh",
        _ => throw new ArgumentException("Reasoning must be none, low, medium, high or xhigh.", nameof(value)),
    };

    private static ReasoningOptions Reasoning(string value) => new()
    {
        Effort = value switch
        {
            "none" => ReasoningEffort.None,
            "low" => ReasoningEffort.Low,
            "medium" => ReasoningEffort.Medium,
            "high" => ReasoningEffort.High,
            "xhigh" => ReasoningEffort.ExtraHigh,
            _ => throw new ArgumentException("Invalid pinned reasoning effort.", nameof(value)),
        },
    };

    public static ChatOptions CreateOptions(ResolvedModel resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        return new()
        {
            ModelId = resolved.Provider is AiProvider.Host ? null : resolved.Model,
            Reasoning = resolved.Reasoning is null ? null : Reasoning(resolved.Reasoning),
            MaxOutputTokens = resolved.MaxOutputTokens,
        };
    }

    // The host client is a singleton; a caller's `using` must not dispose it.
    private sealed class NonDisposing(IChatClient inner) : DelegatingChatClient(inner)
    {
        protected override void Dispose(bool disposing) { }
    }
}
