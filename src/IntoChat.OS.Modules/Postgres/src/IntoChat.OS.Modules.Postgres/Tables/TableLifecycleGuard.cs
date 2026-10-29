using System.Text.Json;
using DigitalBrain.Invocation;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Postgres.Tables;

// Every index and lifetime call names its owner; the guard checks the owner is this brain's and
// the source is the one neuron type allowed to make that call. The table (06-05b) and the index (06-08) are plain neurons: a
// call one makes leaves from its runtime host grain, whose key is its neuron id, and reaches the other's host as a RuntimeInvocation.
// Both are read from the ids Orleans sets on the message (never from the call's body), so the rule is the grain-era one.
internal sealed class TableLifecycleGuard : IIncomingGrainCallFilter
{
    private static readonly string Lifetime = NeuronProxy.ContractName(typeof(ITableLifetime));
    private static readonly string Index = NeuronProxy.ContractName(typeof(ITableIndex));

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        if (context.Request.GetArgumentCount() > 0 && context.Request.GetArgument(0) is RuntimeInvocation call)
        {
            if (string.Equals(call.Contract, Index, StringComparison.Ordinal)) { CheckIndex(context, call); }
            else if (string.Equals(call.Contract, Lifetime, StringComparison.Ordinal)) { CheckLifetime(context, call); }
        }
        await context.Invoke();
    }

    // The index is a plain neuron (06-08): its owner is its neuron id's brain and key, "{brain}/tables/{app}".
    private static void CheckIndex(IIncomingGrainCallContext context, RuntimeInvocation call)
    {
        var owner = OwnerOf(context.TargetId.Key.ToString()!);
        if (owner.BrainId != BrainScope.CurrentId()) { throw new UnauthorizedAccessException("This storage belongs to another brain."); }
        if (call.Method == nameof(ITableIndex.ReadTables))
        {
            var caller = CallerContextStamper.Require();
            if (!CallerContextStamper.IsTrusted(caller) || caller.Kind == IntoChat.OS.Framework.Modules.CallerKind.App)
            { throw new UnauthorizedAccessException("Only trusted host code may read app storage."); }
            return;
        }
        var expected = call.Method is nameof(ITableIndex.Retire) or nameof(ITableIndex.Reopen) ? AppGrainType.Name : TableNeuron.GrainType;
        var source = SourceOf(context.SourceId);
        if (source is null || source.Value.Type != expected)
        { throw new UnauthorizedAccessException("Only the owning module may change table lifetime."); }
        if (call.Method == nameof(ITableIndex.Register)
            && (call.Arguments is not [var argument] || argument.ValueKind != JsonValueKind.String || source.Value.Key != argument.GetString()))
        { throw new UnauthorizedAccessException("A table can only register itself."); }
    }

    private static void CheckLifetime(IIncomingGrainCallContext context, RuntimeInvocation call)
    {
        var source = SourceOf(context.SourceId);
        if (source is null || source.Value.Type != TableIndexNeuron.GrainType)
        { throw new UnauthorizedAccessException("Only the owning module may change table lifetime."); }
        var ownerKey = call.Arguments is [var argument] && argument.ValueKind == JsonValueKind.String ? argument.GetString()! : "";
        var owner = TableOwner.Parse(ownerKey);
        if (owner.BrainId != BrainScope.CurrentId()) { throw new UnauthorizedAccessException("This storage belongs to another brain."); }
        if (source.Value.Key != ownerKey) { throw new UnauthorizedAccessException("This table belongs to another owner."); }
    }

    // The owner a host grain key (the neuron id "brain/postgres.tables/tables/{app}") names; anything else is refused.
    private static TableOwner OwnerOf(string hostKey)
        => DigitalBrain.NeuronId.TryParse(hostKey, out var id) && id.Type == TableIndexNeuron.GrainType
            ? TableOwner.Parse($"{id.Brain.Value}/{id.Key}")
            : throw new UnauthorizedAccessException("This storage key does not name a brain and an app.");

    // The calling grain's type and os key; a runtime host grain (neuron.host*, keyed by the neuron id "brain/type/key") stands
    // for the plain neuron it runs.
    private static (string Type, string Key)? SourceOf(GrainId? source)
    {
        if (source is not { } id) { return null; }
        var type = id.Type.ToString()!;
        var key = id.Key.ToString()!;
        if (type.StartsWith("neuron.host", StringComparison.Ordinal))
        {
            return DigitalBrain.NeuronId.TryParse(key, out var neuron) ? (neuron.Type, $"{neuron.Brain.Value}/{neuron.Key}") : null;
        }
        return (type, key);
    }
}
