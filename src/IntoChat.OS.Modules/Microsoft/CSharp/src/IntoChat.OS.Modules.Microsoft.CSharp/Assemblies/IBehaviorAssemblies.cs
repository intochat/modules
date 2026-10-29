using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;

// Compiled behaviors by content hash; the symbols live beside each assembly as "{hash}.pdb".
public interface IBehaviorAssemblies
{
    Task Put(CompiledBehavior behavior, CancellationToken cancellationToken);
    Task<bool> Exists(string hash, CancellationToken cancellationToken);
    // A URL the sandbox host can GET without credentials for `lifetime`.
    Task<Uri> ReadUrl(string name, TimeSpan lifetime, CancellationToken cancellationToken);
}
