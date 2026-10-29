using System.Runtime.CompilerServices;
using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Tests;

// The model neurons' inference, faked (06-05): every Generate waits until Width generations are inside at once, so overlap is
// measured, not elapsed time; Describe answers at once.
public sealed class GatedInference : IModelInference
{
    private TaskCompletionSource _full = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _width = 1;
    private int _inside;
    private int _peak;

    public int Overlap => Volatile.Read(ref _peak);

    // Starts a measurement: the next generations wait for width of them to be inside.
    public void Expect(int width)
    {
        _width = width;
        _full = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _peak, 0);
    }

    public bool HasProfile(string name) => false;

    public Task<ModelDescriptor> Describe(ModelSelection? selection, Type? marker, CancellationToken cancellationToken, string? brainId)
        => Task.FromResult(new ModelDescriptor("Host", "fake", selection?.Model, "1", CapabilitySupport.Unknown, CapabilitySupport.Unknown, CapabilitySupport.Unknown));

    public async Task<InferenceResult> Generate(InferenceRequest request, Type? marker, CancellationToken cancellationToken, string? brainId)
    {
        var inside = Interlocked.Increment(ref _inside);
        int peak;
        while ((peak = Volatile.Read(ref _peak)) < inside && Interlocked.CompareExchange(ref _peak, inside, peak) != peak) { }
        if (inside >= _width) { _full.TrySetResult(); }
        try { await _full.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); }
        finally { Interlocked.Decrement(ref _inside); }
        return new([new("assistant", [new AiText("ok")])], "stop", null, null, "fake");
    }

    public async IAsyncEnumerable<InferenceUpdate> GenerateStreaming(InferenceRequest request, Type? marker,
        [EnumeratorCancellation] CancellationToken cancellationToken, string? brainId)
    {
        await Task.Yield();
        yield return new("assistant", [new AiText("ok")], "stop", null, null);
    }
}
