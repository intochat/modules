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

public sealed record SharedLibrary(CompiledBehavior? Library, bool HasRules, IReadOnlyList<ScriptDiagnostic> Errors);

public sealed record ScriptCompilation(bool Success, IReadOnlyList<ScriptDiagnostic> Errors, IReadOnlyDictionary<string, CompiledBehavior> Assemblies);

// The single compiler: compiles each behavior in-process against what the sandbox carries and,
// when every file is clean, emits one assembly per file named by its content hash. The sandbox only
// loads what this emits. The implicit console usings plus the script client's namespaces are imported.
// A file without top-level statements is shared: it is compiled into every synapse's assembly and
// yields none itself. A synapse is a file with top-level statements; the split is purely syntactic.
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
    private static readonly CSharpCompilationOptions Options = new(OutputKind.ConsoleApplication, deterministic: true, nullableContextOptions: NullableContextOptions.Enable);
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

    public static bool IsShared(string source)
        => !CSharpSyntaxTree.ParseText(source ?? "", Parse).GetCompilationUnitRoot().Members.OfType<MicrosoftSdk.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax>().Any();

    public ScriptCompilation Compile(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var libraries = files.Where(file => IsShared(file.Value)).OrderBy(file => file.Key, StringComparer.Ordinal).ToArray();
        var programs = files.Where(file => !IsShared(file.Value)).OrderBy(file => file.Key, StringComparer.Ordinal).ToArray();
        if (programs.Length == 0) { throw new ArgumentException("Pass at least one synapse (a file with top-level statements) to compile; files of only type declarations are shared, not programs.", nameof(files)); }
        var errors = new List<ScriptDiagnostic>();
        var assemblies = new Dictionary<string, CompiledBehavior>(StringComparer.Ordinal);
        var shared = libraries.Select(library =>
        {
            ArgumentNullException.ThrowIfNull(library.Value, library.Key);
            return CSharpSyntaxTree.ParseText(SourceText.From(library.Value, Encoding.UTF8), Parse, library.Key);
        }).Append(CSharpSyntaxTree.ParseText(SourceText.From(GlobalUsings, Encoding.UTF8), Parse, "GlobalUsings.cs")).ToArray();
        var reportedLibraryErrors = false;
        var libraryKey = Hash(_references + '\u0002' + string.Join('\u0001', libraries.Select(library => library.Key + "\u0000" + library.Value)));
        foreach (var (path, source) in programs)
        {
            ArgumentNullException.ThrowIfNull(source, path);
            var key = Hash(libraryKey + "\u0001" + path + "\u0000" + source);
            if (_emitted.TryGet(key, out var known)) { assemblies[path] = known; continue; }
            var trees = shared.Prepend(CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), Parse, path)).ToArray();
            var compilation = CSharpCompilation.Create("behavior", trees, References.Value, Options);
            using var assembly = new MemoryStream();
            using var symbols = new MemoryStream();
            Interlocked.Increment(ref _emits);
            var emitted = compilation.Emit(assembly, symbols, options: Emit);
            foreach (var diagnostic in emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                var file = diagnostic.Location.SourceTree?.FilePath ?? path;
                // A library error shows once, not once per program that includes it.
                if (file != path) { if (reportedLibraryErrors) { continue; } }
                errors.Add(new ScriptDiagnostic(file, diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1, diagnostic.GetMessage()));
            }
            reportedLibraryErrors = true;
            if (emitted.Success)
            {
                var bytes = assembly.ToArray();
                var behavior = new CompiledBehavior(bytes, symbols.ToArray(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
                assemblies[path] = behavior;
                _emitted.Set(key, behavior);
            }
        }
        return new(errors.Count == 0, errors, errors.Count == 0 ? assemblies : new Dictionary<string, CompiledBehavior>());
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    // The bundle's shared classes alone, as one library: where its rules (ILogic<T>) live. HasRules says
    // whether any class implements ILogic<>, so a bundle without rules never reaches the sandbox.
    public SharedLibrary CompileShared(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var libraries = files.Where(file => IsShared(file.Value)).OrderBy(file => file.Key, StringComparer.Ordinal).ToArray();
        if (libraries.Length == 0) { return new(null, false, []); }
        var trees = libraries.Select(library => CSharpSyntaxTree.ParseText(SourceText.From(library.Value, Encoding.UTF8), Parse, library.Key))
            .Append(CSharpSyntaxTree.ParseText(SourceText.From(GlobalUsings, Encoding.UTF8), Parse, "GlobalUsings.cs"));
        var compilation = CSharpCompilation.Create("rules", trees, References.Value, Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary));
        using var assembly = new MemoryStream();
        var emitted = compilation.Emit(assembly);
        if (!emitted.Success)
        {
            return new(null, false, [.. emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => new ScriptDiagnostic(diagnostic.Location.SourceTree?.FilePath ?? "", diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1, diagnostic.GetMessage()))]);
        }
        var logic = compilation.GetTypeByMetadataName("IntoChat.OS.ILogic`1");
        var hasRules = logic is not null && Types(compilation.Assembly.GlobalNamespace)
            .Any(type => type.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, logic)));
        var bytes = assembly.ToArray();
        return new(new(bytes, [], Convert.ToHexStringLower(SHA256.HashData(bytes))), hasRules, []);
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space)
        => space.GetTypeMembers().SelectMany(Nested).Concat(space.GetNamespaceMembers().SelectMany(Types));

    private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type)
        => type.GetTypeMembers().SelectMany(Nested).Prepend(type);

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
