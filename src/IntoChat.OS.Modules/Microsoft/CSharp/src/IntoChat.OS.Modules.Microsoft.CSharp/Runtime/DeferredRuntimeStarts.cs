using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// Programs may be installed by silo startup before Kestrel binds its HTTP edge.
// Retry those runtimes once listening starts, through the plain seam (06-08: the runtime is a plain neuron, named by its brain)
// rather than retaining an activation or waiting for its recovery reminder.
internal sealed class DeferredRuntimeStarts(
    IServiceProvider neurons,
    [FromKeyedServices(typeof(CSharpModule))] IHostApplicationLifetime lifetime,
    ILogger<DeferredRuntimeStarts> logger) : BackgroundService
{
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new() { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    // runtime is the brain whose runtime waits.
    public void Defer(string runtime)
    {
        // Keep the key until Touch returns: a still-unavailable edge must not
        // enqueue itself recursively. The durable reminder remains its backstop.
        if (_pending.TryAdd(runtime, 0) && !_queue.Writer.TryWrite(runtime)) { _pending.TryRemove(runtime, out _); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var started = lifetime.ApplicationStarted.Register(() => listening.TrySetResult());
        try
        {
            await listening.Task.WaitAsync(stoppingToken);
            await foreach (var runtime in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try { await SiloNeurons.Get<IBrainRuntime>(neurons, BrainRuntimeKeys.For(runtime)).Touch(stoppingToken).WaitAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error)
                { logger.LogWarning(error, "Could not start deferred runtime {Runtime}; its reminder retries.", runtime); }
                finally { _pending.TryRemove(runtime, out _); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override void Dispose()
    {
        _queue.Writer.TryComplete();
        base.Dispose();
    }
}
