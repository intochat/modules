using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using IntoChat.OS.Modules.AI.Agents.Context;
using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Surface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.AI.Agents;

// Everything one turn needs, settled by the agent before the loop starts.
internal sealed record TurnPlan(string ScopeId, string Conversation, string RunId, AiMessage[] Transcript, string? Summary, AiMessage Input, string Instructions,
    string[] Contracts, string[] Contexts, ModelSelection? Model, InferenceOptions? Options, int MaxModelCalls, bool Streaming);

// Providers propose calls; this is the single, bounded tool invocation loop. It is pulled, not
// pushed: the consumer persists each event before asking for the next, so nothing external starts
// before the agent has written what came before it. A failure becomes the last event; Run never throws.
internal sealed class AgentTurnLoop(IServiceProvider services)
{
    public async IAsyncEnumerable<AgentTurnEvent> Run(TurnPlan plan, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var steps = Steps(plan, ct).GetAsyncEnumerator(ct);
        AgentTurnEvent.Failed? failure = null;
        while (true)
        {
            try { if (!await steps.MoveNextAsync()) { break; } }
            catch (Exception error)
            {
                // Only the run's own token makes a cancellation; a provider or tool timing out is a failure.
                var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
                failure = new(cancelled ? "The run was interrupted." : error.Message, cancelled);
                break;
            }
            yield return steps.Current;
        }
        if (failure is not null) { yield return failure; }
    }

    private async IAsyncEnumerable<AgentTurnEvent> Steps(TurnPlan plan, [EnumeratorCancellation] CancellationToken ct)
    {
        if (plan.MaxModelCalls is < 1 or > 512) { throw new ArgumentOutOfRangeException(nameof(plan), "MaxModelCalls must be between 1 and 512."); }
        if (plan.Contracts.Distinct(StringComparer.Ordinal).Count() != plan.Contracts.Length) { throw new ArgumentException("Contracts must be unique.", nameof(plan)); }
        yield return new AgentTurnEvent.Started(plan.RunId);
        string? currentCall = null;
        var contexts = await ReadContexts(plan.Contexts, ct);
        var surface = services.GetRequiredService<NeuronSurface>();
        // Context may name contracts this host does not deploy; only deployed ones join the turn.
        string[] named = [.. contexts.SelectMany(entry => entry.Contracts).Where(name => !plan.Contracts.Contains(name)).Distinct(StringComparer.Ordinal)];
        var known = named.Length == 0 ? [] : await surface.Contracts(ct);
        string[] composed = [.. plan.Contracts, .. named.Where(known.Contains)];
        // The model's actions are fixed here, once: the neuron surface over the contracts the app composed.
        var tools = composed.Length == 0 ? new List<AIFunction>() : [.. ContractActions.Create(await surface.Restrict(composed, ct), plan.Conversation)];

        var catalog = services.GetRequiredService<ModelCatalog>();
        var selection = plan.Model;
        if (plan.Options?.Reasoning is not null || plan.Options?.MaxOutputTokens is not null)
        {
            selection = (selection ?? new()) with
            { Reasoning = plan.Options.Reasoning ?? selection?.Reasoning, MaxOutputTokens = plan.Options.MaxOutputTokens ?? selection?.MaxOutputTokens };
        }
        var settings = plan.Options is null ? null : plan.Options with { Reasoning = null };
        var resolved = await catalog.Resolve(plan.ScopeId, selection, tools.Count > 0, ct);
        ChatOptions options;
        switch (resolved.Provider)
        {
            case AiProvider.Host:
                // The deployment's own client: provider-neutral settings pass through, provider
                // settings it cannot honor fail here rather than being dropped.
                var hostDescriptor = new ModelDescriptor(resolved.Provider.ToString(), resolved.Model, null,
                    resolved.Revision, CapabilitySupport.Supported, CapabilitySupport.Supported, CapabilitySupport.Supported,
                    Temperature: CapabilitySupport.Supported, TopP: CapabilitySupport.Supported);
                options = InferenceMapping.CreateOptions(resolved, settings, hostDescriptor);
                yield return new AgentTurnEvent.ModelSelected(resolved, hostDescriptor);
                break;
            default:
                var descriptor = catalog.Describe(resolved);
                InferenceMapping.ValidateRequest(new InferenceRequest([.. plan.Transcript, plan.Input], selection, plan.Options), descriptor);
                options = InferenceMapping.CreateOptions(resolved, settings, descriptor);
                yield return new AgentTurnEvent.ModelSelected(resolved, descriptor);
                break;
        }
        using var client = await catalog.CreateClient(plan.ScopeId, resolved, ct);
        options.Instructions = plan.Instructions;
        options.Tools = [.. tools];
        var messages = new List<ChatMessage>();
        if (plan.Summary is { Length: > 0 }) { messages.Add(new(ChatRole.System, "Earlier conversation summary: " + plan.Summary)); }
        messages.AddRange(plan.Transcript.Select(InferenceMapping.ToChatMessage));
        foreach (var entry in contexts.Where(entry => entry.Text.Length > 0)) { messages.Add(new(ChatRole.System, entry.Text)); }
        messages.Add(InferenceMapping.ToChatMessage(plan.Input));
        var generated = new List<ChatMessage>();
        UsageDetails? usage = null;
        var calls = new HashSet<string>(StringComparer.Ordinal);
        for (var step = 0; step < plan.MaxModelCalls; step++)
        {
            ChatResponse response;
            if (plan.Streaming)
            {
                var updates = new List<ChatResponseUpdate>();
                await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct))
                {
                    updates.Add(update);
                    if (!string.IsNullOrEmpty(update.Text)) { yield return new AgentTurnEvent.Text(update.Text); }
                }
                response = updates.ToChatResponse();
            }
            else
            {
                response = await client.GetResponseAsync(messages, options, ct);
                if (!string.IsNullOrEmpty(response.Text)) { yield return new AgentTurnEvent.Text(response.Text); }
            }
            ct.ThrowIfCancellationRequested();
            if (response.Usage is not null) { (usage ??= new()).Add(response.Usage); }
            if (response.Messages.Count == 0) { throw new InvalidOperationException("The provider returned no messages."); }
            messages.AddRange(response.Messages);
            generated.AddRange(response.Messages);
            var requested = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToArray();
            if (requested.Length == 0)
            {
                yield return new AgentTurnEvent.Completed([.. generated.Select(InferenceMapping.FromChatMessage)],
                    usage is null ? null : new(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount));
                yield return new AgentTurnEvent.Finished();
                yield break;
            }
            foreach (var call in requested)
            {
                if (string.IsNullOrWhiteSpace(call.CallId) || !calls.Add(call.CallId)) { throw new InvalidOperationException("The model reused or omitted a tool call identity."); }
                // The consumer persists the started call before pulling the next event, so the tool never runs unrecorded.
                yield return new AgentTurnEvent.ToolStarted(call.CallId, call.Name, JsonSerializer.Serialize(call.Arguments));
                var tool = tools.SingleOrDefault(item => item.Name == call.Name);
                if (tool is null)
                {
                    var unavailable = new
                    {
                        isError = true,
                        code = "tool_unavailable",
                        tool = call.Name,
                        message = "This tool is not available in this turn.",
                    };
                    var refusal = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, unavailable)]);
                    messages.Add(refusal);
                    generated.Add(refusal);
                    yield return new AgentTurnEvent.ToolCompleted(call.CallId, call.Name, JsonSerializer.Serialize(unavailable));
                    continue;
                }
                currentCall = call.CallId;
                object? result = null;
                var serialized = "";
                ExceptionDispatchInfo? fault = null;
                try
                {
                    result = await tool.InvokeAsync(new AIFunctionArguments(call.Arguments ?? new Dictionary<string, object?>()), ct);
                    ct.ThrowIfCancellationRequested();
                    serialized = JsonSerializer.Serialize(result);
                }
                catch (Exception error) { fault = ExceptionDispatchInfo.Capture(error); }
                finally { currentCall = null; }
                // An iterator cannot yield from a catch: the failure is reported here, then ends the turn as it failed.
                if (fault is not null)
                {
                    var cancelled = fault.SourceException is OperationCanceledException;
                    yield return new AgentTurnEvent.ToolFailed(call.CallId, call.Name, cancelled ? "tool_cancelled" : "tool_failed",
                        cancelled ? "The tool call was interrupted." : fault.SourceException.Message);
                    fault.Throw();
                }
                var resultMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]);
                messages.Add(resultMessage);
                generated.Add(resultMessage);
                yield return new AgentTurnEvent.ToolCompleted(call.CallId, call.Name, serialized);
            }
        }
        throw new InvalidOperationException("The agent reached its model-call budget.");
    }

    // A context that is missing or fails leaves the turn without it rather than failing the turn.
    private async Task<List<ContextEntry>> ReadContexts(string[] keys, CancellationToken ct)
    {
        if (keys.Length == 0) { return []; }
        // A context is a plain neuron (06-05), reached by its os key "{brain}/{key}".
        var plain = services;
        async Task<IEnumerable<ContextEntry>> Read(string key)
        {
            try { return (await SiloNeurons.Get<IAgentContext>(services, key).Read().WaitAsync(ct)).Entries; }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                services.GetService<ILoggerFactory>()?.CreateLogger<AgentTurnLoop>().LogWarning(error, "Context {Context} could not be read; the turn continues without it.", key);
                return [];
            }
        }
        // The reads overlap; Task.WhenAll keeps the results in the order of keys.
        return [.. (await Task.WhenAll(keys.Select(Read))).SelectMany(entries => entries)];
    }
}
