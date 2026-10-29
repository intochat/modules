using System.Security.Cryptography;
using DigitalBrain.Runtime.Orleans;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;

// Local sandboxes download the same content-addressed pair as cloud sandboxes, with an expiring grant for one file.
internal sealed class LocalBehaviorAssemblies : IBehaviorAssemblies
{
    internal const string ProtectionPurpose = "IntoChat.OS.CSharp.BehaviorDownload.v1";
    private const string Route = "/runtime/v1/assemblies/";
    private readonly string _root;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IServiceProvider _services;
    private readonly IOptions<CSharpOptions> _options;

    public LocalBehaviorAssemblies(IConfiguration configuration, IDataProtectionProvider protection, IServiceProvider services, IOptions<CSharpOptions> options)
    {
        _root = Path.Combine(StateHome.Resolve(configuration).Path, "behaviors");
        _protector = protection.CreateProtector(ProtectionPurpose).ToTimeLimitedDataProtector();
        _services = services;
        _options = options;
    }

    public async Task Put(CompiledBehavior behavior, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        RequireName(behavior.Hash);
        Directory.CreateDirectory(_root);
        await WriteAsync(behavior.Hash, behavior.Assembly, cancellationToken).ConfigureAwait(false);
        // As with the blob store, symbols are written last: their presence marks the pair complete.
        await WriteAsync(behavior.Hash + ".pdb", behavior.Symbols, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> Exists(string hash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireName(hash);
        return Task.FromResult(File.Exists(Path.Combine(_root, hash)) && File.Exists(Path.Combine(_root, hash + ".pdb")));
    }

    public Task<Uri> ReadUrl(string name, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireName(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        var root = _options.Value.EdgeUrl;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = _services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
                .FirstOrDefault(address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        }
        if (string.IsNullOrWhiteSpace(root)) { throw new InvalidOperationException("The local behavior store needs the kernel HTTP edge to listen before issuing download URLs."); }
        root = root.Replace("://+", "://localhost", StringComparison.Ordinal).Replace("://*", "://localhost", StringComparison.Ordinal)
            .Replace("[::]", "localhost", StringComparison.Ordinal).Replace("0.0.0.0", "localhost", StringComparison.Ordinal);
        var edge = BrainRuntimeNeuron.Reachable(new Uri(root.TrimEnd('/') + "/"));
        return Task.FromResult(new Uri(edge, Route.TrimStart('/') + name + "?token=" + Uri.EscapeDataString(_protector.Protect(name, lifetime))));
    }

    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet(Route + "{name}", (string name, string? token, [FromServices] IBehaviorAssemblies store)
            => store is LocalBehaviorAssemblies local ? local.Download(name, token) : Results.NotFound())
            // The expiring file grant authenticates this endpoint, as a blob SAS does; account cookies do not.
            .AllowAnonymous().ExcludeFromDescription();

    private IResult Download(string name, string? token)
    {
        if (!ValidName(name) || string.IsNullOrEmpty(token)) { return Results.Unauthorized(); }
        try
        {
            if (_protector.Unprotect(token) != name) { return Results.Unauthorized(); }
        }
        catch (CryptographicException) { return Results.Unauthorized(); }
        catch (FormatException) { return Results.Unauthorized(); }
        var path = Path.Combine(_root, name);
        return File.Exists(path) ? Results.File(path, "application/octet-stream") : Results.NotFound();
    }

    private async Task WriteAsync(string name, byte[] bytes, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_root, name);
        if (File.Exists(path))
        {
            await RequireSameBytes(path, bytes, cancellationToken).ConfigureAwait(false);
            return;
        }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path))
            {
                // Another restore published this immutable file while our temporary copy was being written.
                await RequireSameBytes(path, bytes, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { File.Delete(temporary); }
    }

    private static async Task RequireSameBytes(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var existing = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (!existing.AsSpan().SequenceEqual(bytes))
        { throw new InvalidDataException($"Behavior cache entry '{Path.GetFileName(path)}' contains different bytes."); }
    }

    private static bool ValidName(string name)
        => (name.Length == 64 || (name.Length == 68 && name.EndsWith(".pdb", StringComparison.Ordinal)))
            && name[..64].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireName(string name)
    {
        if (!ValidName(name)) { throw new ArgumentException("A behavior download names a SHA-256 hash or its .pdb file.", nameof(name)); }
    }
}
