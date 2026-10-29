using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Spi;
using IntoChat.OS.Framework;
using IntoChat.OS.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Flutter.Workspace;

// A host edge outside any neuron: proxies address os full keys, the observer lists the brain, the watch is the session synapse.
internal sealed class BrainEdge(INeuronProxies proxies, IBrainObserver kernel, ISignalWatch watch)
{
    public IBrainObserver Kernel => kernel;

    public T Get<T>(string osFullKey) where T : class => proxies.Get<T>(osFullKey);

    public IBrain Self(string brainId) => proxies.Get<IBrain>(IBrain.Key(brainId));

    public IAsyncEnumerable<T> On<T>(object neuron, Func<Task> onSubscribed, CancellationToken cancellationToken = default) where T : DigitalBrain.Signal
        => watch.On<T>(neuron, onSubscribed, cancellationToken);

    public static BrainEdge Open(IServiceProvider services, string brainId)
        => new(services.GetRequiredService<INeuronProxies>(), services.GetRequiredService<IBrainObserver>(),
            services.GetRequiredService<IIntoChatOSBrains>().Open(brainId));
}
