using System.ComponentModel;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Surface;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI.Agents;

// The model's only actions: the neuron surface over the contracts the app composed. Each call runs
// as the app, marked model-originated so neurons can withhold what the model must not see.
internal static class ContractActions
{
    public static IReadOnlyList<AIFunction> Create(NeuronSurface surface, string conversation) =>
    [
        AIFunctionFactory.Create(([Description("Optional full contract type name to list one type only.")] string? contract = null, CancellationToken ct = default) =>
            Marked(conversation, async () => await surface.List(contract, ct)), NeuronSurfaceTools.ListName, NeuronSurfaceTools.ListDescription),
        AIFunctionFactory.Create(([Description("What you want to do.")] string query, CancellationToken ct) =>
            Marked(conversation, async () => await surface.Search(query, ct)), NeuronSurfaceTools.SearchName, NeuronSurfaceTools.SearchDescription),
        AIFunctionFactory.Create((string contract, string key, CancellationToken ct) =>
            Marked(conversation, async () => await surface.Get(contract, key, ct)), NeuronSurfaceTools.GetName, NeuronSurfaceTools.GetDescription),
        AIFunctionFactory.Create((string contract, string key, string method, string arguments, CancellationToken ct) =>
            Marked(conversation, async () => (object?)await surface.Call(contract, key, method, arguments, ct) ?? new { ok = true }),
            NeuronSurfaceTools.CallName, NeuronSurfaceTools.CallDescription),
        AIFunctionFactory.Create(() => Marked(conversation, () => Task.FromResult<object>(CallerContextStamper.Require())),
            NeuronSurfaceTools.ContextName, NeuronSurfaceTools.ContextDescription),
    ];

    private static async Task<object> Marked(string conversation, Func<Task<object>> action)
    {
        var caller = CallerContextStamper.Require();
        CallerContextStamper.Stamp(caller with { ConversationId = conversation });
        try { return await action(); }
        catch (Exception error) when (error is ArgumentException or KeyNotFoundException or UnauthorizedAccessException or InvalidOperationException)
        { return new { isError = true, message = error.Message }; }
        finally { CallerContextStamper.Stamp(caller); }
    }
}
