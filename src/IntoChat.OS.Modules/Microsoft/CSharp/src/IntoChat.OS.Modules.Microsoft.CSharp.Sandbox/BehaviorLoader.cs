using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// Downloads a behavior's assembly by hash into a content-addressed cache, refuses bytes that do not
// hash to their name, and runs it in its own collectible load context.
public sealed class BehaviorLoader(HttpClient downloads, IOptions<SandboxOptions> options)
{
    public TimeSpan DrainDeadline => options.Value.DrainDeadline;

    // Fetches and verifies an assignment's assembly into the cache without running it, so a reassign knows the new build is good
    // before it stops the old one.
    public async Task PrepareAsync(AssignmentEvent assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await CachedAsync(assignment.Hash, assignment.Assembly, verify: true, cancellationToken).ConfigureAwait(false);
        await CachedAsync(assignment.Hash + ".pdb", assignment.Symbols, verify: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BehaviorRun> LoadAsync(SessionConnection session, AssignmentEvent assignment, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var assembly = await CachedAsync(assignment.Hash, assignment.Assembly, verify: true, cancellationToken).ConfigureAwait(false);
        var symbols = await CachedAsync(assignment.Hash + ".pdb", assignment.Symbols, verify: false, cancellationToken).ConfigureAwait(false);
        return Load(session, assignment, assembly, symbols, logger);
    }

    // Collectible types stay on this synchronous stack, never in the completed download task's state.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private BehaviorRun Load(SessionConnection session, AssignmentEvent assignment, byte[] assembly, byte[] symbols, ILogger logger)
    {
        var context = new BehaviorContext(assignment.Behavior);
        try
        {
            using var image = new MemoryStream(assembly);
            // A package built for release carries no symbols.
            using var pdb = new MemoryStream(symbols);
            var loaded = symbols.Length == 0 ? context.LoadFromStream(image) : context.LoadFromStream(image, pdb);
            return BehaviorRun.Start(session, assignment, context, loaded, options.Value.LogLines, logger);
        }
        catch { context.Unload(); throw; }
    }

    internal async Task<byte[]> CachedAsync(string name, Uri url, bool verify, CancellationToken cancellationToken)
    {
        var path = Path.Combine(options.Value.CacheRoot, name);
        if (File.Exists(path))
        {
            var cached = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (!verify || Matches(name, cached)) { return cached; }
        }
        var bytes = await downloads.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
        if (verify && !Matches(name, bytes)) { throw new InvalidDataException($"The assembly downloaded for {name} does not hash to its name; refusing to load it."); }
        Directory.CreateDirectory(options.Value.CacheRoot);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    private static bool Matches(string hash, byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes)) == hash;

    // Collectible for unloading and crash containment, not for security: behaviors of a brain trust each
    // other. Everything the host already carries (the client, the contracts, the framework) is shared from
    // the default context, so a behavior's types are the host's types; anything else in the host's
    // directory is loaded there once.
    internal sealed class BehaviorContext(string behavior) : AssemblyLoadContext("behavior " + behavior, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (Default.Assemblies.FirstOrDefault(loaded => AssemblyName.ReferenceMatchesDefinition(name, loaded.GetName())) is { } shared) { return shared; }
            var candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? Default.LoadFromAssemblyPath(candidate) : null;
        }
    }
}
