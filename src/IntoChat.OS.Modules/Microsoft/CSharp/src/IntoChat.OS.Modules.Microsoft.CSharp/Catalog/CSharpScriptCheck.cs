using System.Security.Cryptography;
using System.Text;
using IntoChat.OS.Connections.Caching;
using IntoChat.OS.Client.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

using MicrosoftSdk = Microsoft;
namespace IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

public sealed record ScriptDiagnostic(string File, string Id, int Line, string Message);

public sealed record CompiledBehavior(byte[] Assembly, byte[] Symbols, string Hash);

public sealed record ScriptCompilation(bool Success, IReadOnlyList<ScriptDiagnostic> Errors, IReadOnlyDictionary<string, CompiledBehavior> Assemblies);

// The single compiler: compiles all application files in a bundle into a single dynamically-linked library (DLL)
// assembly against what the sandbox carries. Enforces that exactly one class decorated with [App] is declared.
public sealed class CSharpScriptCheck
{
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using IntoChat.OS.Client;
        global using IntoChat.OS;
        global using DigitalBrain;
        """;

    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview);
    private static readonly Lazy<MetadataReference[]> References = new(LoadReferences);
    private static readonly Lazy<string> ReferenceFingerprint = new(() => Hash(string.Join('\u0001', References.Value
        .Select(reference => reference.Display + "\u0000" + (reference is PortableExecutableReference { FilePath: { } path } ? new FileInfo(path).Length + ":" + File.GetLastWriteTimeUtc(path).Ticks : "")))));
    // Deterministic so one source always yields one hash: no PDB path, no timestamps.
    private static readonly CSharpCompilationOptions Options = new(OutputKind.DynamicallyLinkedLibrary, deterministic: true, nullableContextOptions: NullableContextOptions.Enable);
    private static readonly EmitOptions Emit = new(debugInformationFormat: DebugInformationFormat.PortablePdb);

    public const int CacheCapacity = 1024;
    // Owned by whoever builds the check (the module registers one singleton), keyed by the reference fingerprint, the
    // synapse's path and source plus every shared file: the whole input of an emit. Only clean emits are kept; the least
    // recently used one goes when the cache is full.
    private readonly BoundedCache<string, CompiledBehavior> _emitted;
    private long _emits;
    private readonly string _references;

    public CSharpScriptCheck() : this(ReferenceFingerprint.Value, new(CacheCapacity, StringComparer.Ordinal)) { }

    public CSharpScriptCheck(BoundedCache<string, CompiledBehavior> emitted) : this(ReferenceFingerprint.Value, emitted) { }

    public CSharpScriptCheck(string referencesFingerprint) : this(referencesFingerprint, new(CacheCapacity, StringComparer.Ordinal)) { }

    // The fingerprint of the contract set the emits compile against; part of every cache key.
    public CSharpScriptCheck(string referencesFingerprint, BoundedCache<string, CompiledBehavior> emitted)
    {
        _references = referencesFingerprint;
        _emitted = emitted;
    }

    public long Emits => Interlocked.Read(ref _emits);

    public ScriptCompilation Compile(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("Pass at least one file to compile.", nameof(files));
        }

        var trees = files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
        {
            ArgumentNullException.ThrowIfNull(file.Value, file.Key);
            return CSharpSyntaxTree.ParseText(SourceText.From(file.Value, Encoding.UTF8), Parse, file.Key);
        }).Append(CSharpSyntaxTree.ParseText(SourceText.From(GlobalUsings, Encoding.UTF8), Parse, "GlobalUsings.cs")).ToArray();

        var bundleKey = Hash(_references + '\u0002' + string.Join('\u0001', files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => f.Key + "\u0000" + f.Value)));

        var compilation = CSharpCompilation.Create("app", trees, References.Value, Options);

        var appAttribute = compilation.GetTypeByMetadataName("DigitalBrain.AppAttribute");
        var appClasses = new List<INamedTypeSymbol>();
        CollectAppClasses(compilation.GlobalNamespace, appClasses, appAttribute);

        var errors = new List<ScriptDiagnostic>();
        if (appClasses.Count == 0)
        {
            var file = files.Keys.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault() ?? "";
            errors.Add(new ScriptDiagnostic(file, "DB0030", 1, "The application must declare exactly one class decorated with [App]."));
        }
        else if (appClasses.Count > 1)
        {
            var names = string.Join(", ", appClasses.Select(c => c.Name));
            var second = appClasses[1];
            var loc = second.Locations.FirstOrDefault();
            var file = loc?.SourceTree?.FilePath ?? "";
            var line = loc?.GetLineSpan().StartLinePosition.Line + 1 ?? 1;
            errors.Add(new ScriptDiagnostic(file, "DB0030", line, $"The application must declare exactly one class decorated with [App], but found {appClasses.Count}: {names}."));
        }
        else
        {
            var appClass = appClasses[0];
            if (appClass.IsAbstract || appClass.IsGenericType)
            {
                var loc = appClass.Locations.FirstOrDefault();
                var file = loc?.SourceTree?.FilePath ?? "";
                var line = loc?.GetLineSpan().StartLinePosition.Line + 1 ?? 1;
                errors.Add(new ScriptDiagnostic(file, "DB0030", line, "The [App] class cannot be abstract or generic."));
            }
        }

        var appFile = appClasses.Count > 0 ? (appClasses[0].Locations.FirstOrDefault()?.SourceTree?.FilePath ?? files.Keys.OrderBy(k => k, StringComparer.Ordinal).First()) : files.Keys.OrderBy(k => k, StringComparer.Ordinal).First();

        if (errors.Count == 0 && _emitted.TryGet(bundleKey, out var known))
        {
            return new(true, [], new Dictionary<string, CompiledBehavior>(StringComparer.Ordinal) { [appFile] = known });
        }

        using var assembly = new MemoryStream();
        using var symbols = new MemoryStream();
        Interlocked.Increment(ref _emits);
        var emitted = compilation.Emit(assembly, symbols, options: Emit);

        foreach (var diagnostic in emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
        {
            var file = diagnostic.Location.SourceTree?.FilePath ?? "";
            errors.Add(new ScriptDiagnostic(file, diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1, diagnostic.GetMessage()));
        }

        if (errors.Count == 0 && emitted.Success)
        {
            var bytes = assembly.ToArray();
            var behavior = new CompiledBehavior(bytes, symbols.ToArray(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            _emitted.Set(bundleKey, behavior);
            return new(true, [], new Dictionary<string, CompiledBehavior>(StringComparer.Ordinal) { [appFile] = behavior });
        }

        return new(false, errors, new Dictionary<string, CompiledBehavior>(StringComparer.Ordinal));
    }

    private static void CollectAppClasses(INamespaceOrTypeSymbol container, List<INamedTypeSymbol> appClasses, INamedTypeSymbol? appAttribute)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                CollectAppClasses(childNs, appClasses, appAttribute);
            }
            else if (member is INamedTypeSymbol type)
            {
                if (type.TypeKind == TypeKind.Class && HasAppAttribute(type, appAttribute))
                {
                    appClasses.Add(type);
                }
                CollectAppClasses(type, appClasses, appAttribute);
            }
        }
    }

    private static bool HasAppAttribute(INamedTypeSymbol type, INamedTypeSymbol? appAttribute)
        => type.GetAttributes().Any(attr =>
            (appAttribute is not null && SymbolEqualityComparer.Default.Equals(attr.AttributeClass, appAttribute))
            || attr.AttributeClass?.Name is "AppAttribute" or "App");

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    // Exactly what the sandbox image carries: the .NET runtime, plus the vocabulary, the client and every
    // contract assembly with what they depend on. Nothing else this host loads (the framework ring, the
    // platform, drivers, the compiler) is a behavior's to reach; it would compile here and fail to load there.
    // The assemblies a script compiles against, by simple name.
    /// <summary>The assembly names a script may reference; read-only, pinned by ScriptVisibilityFacts.</summary>
    public static IReadOnlyCollection<string> ReferenceNames()
        => [.. References.Value.Select(reference => Path.GetFileNameWithoutExtension(((PortableExecutableReference)reference).FilePath)!)];

    private static MetadataReference[] LoadReferences()
    {
        var available = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .DistinctBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, StringComparer.OrdinalIgnoreCase);
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var carried = new HashSet<string>(available.Where(entry => Path.GetDirectoryName(entry.Value) == runtime).Select(entry => entry.Key), StringComparer.OrdinalIgnoreCase);
        // A contract assembly is a *.Contracts project or a module assembly that declares it hosts its own contracts;
        // the core vocabulary packages DigitalBrain and DigitalBrain.UI are seeded with IntoChat.OS.
        var pending = new Stack<string>(available.Where(entry => entry.Key is "IntoChat.OS" or "IntoChat.OS.Client" or "DigitalBrain" or "DigitalBrain.UI"
            || entry.Key.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase)
            || (entry.Key.StartsWith("IntoChat.OS.Modules.", StringComparison.OrdinalIgnoreCase) && ContractsHost.Declares(entry.Value))).Select(entry => entry.Key));
        while (pending.TryPop(out var name))
        {
            if (!carried.Add(name)) { continue; }
            // A hosting module assembly is carried for its contract surface only; its implementation's dependencies are not a script's to reach.
            if (name.StartsWith("IntoChat.OS.Modules.", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase)) { continue; }
            using var image = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(available[name]));
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(image);
            foreach (var handle in metadata.AssemblyReferences)
            {
                var dependency = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                if (available.ContainsKey(dependency) && !carried.Contains(dependency)) { pending.Push(dependency); }
            }
        }
        return [.. carried.Select(name => (MetadataReference)MetadataReference.CreateFromFile(available[name]))];
    }
}
