using System.Runtime.CompilerServices;
using System.Text.Json;
using IntoChat.OS.Modules.AI.Inference;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI.Models;

public sealed class InferenceService(ModelCatalog catalog) : IModelInference
{
    bool IModelInference.HasProfile(string name) => HasProfile(name);

    Task<ModelDescriptor> IModelInference.Describe(ModelSelection? selection, Type? marker, CancellationToken cancellationToken, string? brainId)
        => Describe(selection, marker, cancellationToken, brainId);

    Task<InferenceResult> IModelInference.Generate(InferenceRequest request, Type? marker, CancellationToken cancellationToken, string? brainId)
        => Generate(request, marker, cancellationToken, brainId);

    IAsyncEnumerable<InferenceUpdate> IModelInference.GenerateStreaming(InferenceRequest request, Type? marker, CancellationToken cancellationToken, string? brainId)
        => GenerateStreaming(request, marker, cancellationToken, brainId);

    internal bool HasProfile(string name) => catalog.HasProfile(name);

    // A typed model neuron pins its marker: without a selection it resolves the marker, with one the
    // selection must land on the same provider and model.
    private async Task<ResolvedModel> ResolveFor(string? brainId, ModelSelection? selection, Type? marker, bool tools, CancellationToken ct)
    {
        var preset = marker is null ? null : LLMModel.FindByMarker(marker)
            ?? throw new ArgumentException($"Unknown model marker {marker.Name}.", nameof(marker));
        if (preset is not null && string.IsNullOrWhiteSpace(selection?.Model))
        { selection = (selection ?? new()) with { Model = preset.Marker.Name }; }
        var resolved = await catalog.Resolve(brainId, selection, tools, ct);
        if (preset is not null)
        {
            var expected = await catalog.Resolve(brainId, new(preset.Marker.Name), requiresTools: false, ct);
            if (resolved.Provider != expected.Provider || resolved.Model != expected.Model)
            { throw new ArgumentException($"The selected profile does not match typed model {preset.Marker.Name}.", nameof(selection)); }
        }
        return resolved;
    }

    public async Task<ModelDescriptor> Describe(ModelSelection? selection = null, Type? marker = null, CancellationToken cancellationToken = default, string? brainId = null)
        => catalog.Describe(await ResolveFor(brainId, selection, marker, false, cancellationToken));

    public async Task<InferenceResult> Generate(InferenceRequest request, Type? marker = null, CancellationToken cancellationToken = default, string? brainId = null)
    {
        var (model, messages, options) = await Prepare(brainId, request, marker, cancellationToken);
        using var client = await catalog.CreateClient(brainId, model, cancellationToken);
        var result = await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        return new(result.Messages.Select(InferenceMapping.FromChatMessage).ToArray(), result.FinishReason?.Value,
            InferenceMapping.Usage(result.Usage), result.ResponseId, result.ModelId ?? model.Model);
    }

    public async IAsyncEnumerable<InferenceUpdate> GenerateStreaming(InferenceRequest request, Type? marker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, string? brainId = null)
    {
        var (model, messages, options) = await Prepare(brainId, request, marker, cancellationToken);
        using var client = await catalog.CreateClient(brainId, model, cancellationToken);
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            yield return new(update.Role?.Value, update.Contents.Where(c => c is not UsageContent).Select(InferenceMapping.FromContent).ToArray(),
                update.FinishReason?.Value, InferenceMapping.Usage(update.Contents.OfType<UsageContent>().LastOrDefault()?.Details),
                update.ResponseId, update.MessageId, update.ModelId ?? model.Model);
        }
    }

    private async Task<(ResolvedModel Model, List<ChatMessage> Messages, ChatOptions Options)> Prepare(string? brainId, InferenceRequest request, Type? marker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages is not { Count: > 0 }) { throw new ArgumentException("At least one message is required.", nameof(request)); }
        var selection = request.Model;
        if (request.Options?.Reasoning is not null || request.Options?.MaxOutputTokens is not null)
        {
            selection = (selection ?? new()) with
            {
                Reasoning = request.Options.Reasoning ?? selection?.Reasoning,
                MaxOutputTokens = request.Options.MaxOutputTokens ?? selection?.MaxOutputTokens,
            };
        }
        var model = await ResolveFor(brainId, selection, marker, request.Tools is { Count: > 0 }, cancellationToken);
        var descriptor = catalog.Describe(model);
        InferenceMapping.ValidateRequest(request, descriptor);
        var options = InferenceMapping.CreateOptions(model, request.Options is null ? null : request.Options with { Reasoning = null }, descriptor);
        if (request.Tools is { Count: > 0 })
        {
            if (request.Tools.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count() != request.Tools.Count)
            { throw new ArgumentException("Tool names must be unique.", nameof(request)); }
            options.Tools = request.Tools.Select(tool => (AITool)AIFunctionFactory.CreateDeclaration(tool.Name, tool.Description,
                JsonSerializer.Deserialize<JsonElement>(tool.ParametersJson))).ToList();
        }
        return (model, request.Messages.Select(InferenceMapping.ToChatMessage).ToList(), options);
    }
}
