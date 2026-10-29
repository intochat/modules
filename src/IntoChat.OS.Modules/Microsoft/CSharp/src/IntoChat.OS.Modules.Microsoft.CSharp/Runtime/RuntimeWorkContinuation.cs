using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

internal sealed class RuntimeWorkContinuation(IServiceProvider neurons) : IWorkContinuation
{
    public Task Acquire(string brainScope, string token)
        => SiloNeurons.Get<IBrainRuntimeContinuations>(neurons, BrainRuntimeKeys.For(brainScope)).Acquire(token);

    public Task Release(string brainScope, string token)
        => SiloNeurons.Get<IBrainRuntimeContinuations>(neurons, BrainRuntimeKeys.For(brainScope)).Release(token);
}
