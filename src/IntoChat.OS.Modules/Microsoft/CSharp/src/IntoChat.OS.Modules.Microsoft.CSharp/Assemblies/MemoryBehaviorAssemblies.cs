using System.Collections.Concurrent;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;

// For hosts without durable storage (unit and in-memory hosts): they compile and record behaviors
// but run no sandbox, so a read URL names the hash without serving it.
internal sealed class MemoryBehaviorAssemblies : IBehaviorAssemblies
{
    public ConcurrentDictionary<string, CompiledBehavior> Stored { get; } = new(StringComparer.Ordinal);

    public Task Put(CompiledBehavior behavior, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        Stored[behavior.Hash] = behavior;
        return Task.CompletedTask;
    }

    public Task<bool> Exists(string hash, CancellationToken cancellationToken) => Task.FromResult(Stored.ContainsKey(hash));

    public Task<Uri> ReadUrl(string name, TimeSpan lifetime, CancellationToken cancellationToken)
        => Task.FromResult(new Uri("memory://behaviors/" + name));
}
