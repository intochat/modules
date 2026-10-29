using System.Runtime.CompilerServices;
using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain;
using DigitalBrain.Invocation;

namespace IntoChat.OS.Modules.AI.Models;

// A brain's model calls run concurrently: only Describe and Generate interleave, with each other (streams always interleave on
// the host). Publishes take one gate, so the inference signals of interleaved calls are journaled one at a time.
[DigitalBrain.InterleavesWhen(nameof(Interleaves))]
internal abstract class LlmNeuronBase(IModelInference inference, Type? marker = null) : StatelessAiNeuron, ILLM
{
    private readonly SemaphoreSlim _publishing = new(1, 1);

    public static bool Interleaves(RuntimeInvocation request)
        => request.Method is nameof(ILLM.Generate) or nameof(ILLM.Describe);

    public Task<ModelDescriptor> Describe(ModelSelection? selection = null)
        => inference.Describe(Selection(selection), marker, CancellationToken.None, Brain);
    public async Task<InferenceResult> Generate(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid().ToString("N");
        var key = FullKey;
        await Signal(new InferenceStarted(key, id));
        try
        {
            var result = await inference.Generate(request with { Model = Selection(request.Model) }, marker, cancellationToken, Brain);
            await Signal(new InferenceCompleted(key, id));
            return result;
        }
        catch (Exception error)
        {
            await Signal(new InferenceFailed(key, id, error is OperationCanceledException ? "Cancelled" : error.GetType().Name));
            throw;
        }
    }
    public async IAsyncEnumerable<InferenceUpdate> GenerateStreaming(InferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid().ToString("N");
        var key = FullKey;
        var completed = false;
        await Signal(new InferenceStarted(key, id));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await foreach (var update in inference.GenerateStreaming(request with { Model = Selection(request.Model) }, marker, lifetime.Token, Brain))
            { yield return update; }
            completed = true;
        }
        finally
        {
            await lifetime.CancelAsync();
            if (completed) { await Signal(new InferenceCompleted(key, id)); }
            else { await Signal(new InferenceFailed(key, id, "Stream cancelled, failed or abandoned")); }
        }
    }

    // R4-03: the former Watch/Unwatch overrides only took the publish gate so the observer set never changed under a running
    // notify; the runtime's synapse engine owns the subscribers now, so that gate has nothing left to guard. Who may subscribe is
    // the model key's brain: the inference signals journal into it, while ILLM itself serves callers of every brain ([Shared]).
    protected override Task AdmitSynapse(DigitalBrain.NeuronId subscriber, IReadOnlyList<Type> signals)
    {
        if (subscriber.Brain != Id.Brain)
        { throw new UnauthorizedAccessException($"Neuron {subscriber} of another brain may not subscribe to the model {Id}."); }
        return Task.CompletedTask;
    }

    private async Task Signal(DigitalBrain.Signal signal)
    {
        await _publishing.WaitAsync();
        try { await Publish(signal); }
        finally { _publishing.Release(); }
    }

    // The brain this model neuron serves, from its key; its provider keys apply to these calls.
    private string Brain => Id.Brain.Value;

    private ModelSelection? Selection(ModelSelection? selection)
    {
        if (selection is not null) { return selection; }
        // A brain's model key ("{brain}/models") names no profile: the deployment default (or the marker's model). Any other key
        // names a profile by its key within the brain ("{brain}/{profile}"; 06-05: a plain key always names its brain, where a
        // grain's bare key "generic" named the profile alone).
        var unnamed = FullKey == ModelAddress.Key(Brain);
        return unnamed || (marker is not null && !inference.HasProfile(Id.Key)) ? null : new(Id.Key);
    }
}

[Neuron("ai.llm")]
internal sealed class LlmNeuron(IModelInference inference) : LlmNeuronBase(inference);
