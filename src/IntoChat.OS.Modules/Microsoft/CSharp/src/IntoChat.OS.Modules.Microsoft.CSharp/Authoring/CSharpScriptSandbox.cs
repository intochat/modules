using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Authoring;

public sealed class CSharpScriptSandbox(CSharpToolService tools, IBehaviorAssemblies assemblies) : IScriptSandbox
{
    public CSharpScriptSandbox(CSharpToolService tools, IBehaviorAssemblies assemblies, IServiceProvider services, IOptions<CSharpOptions> options) : this(tools, assemblies)
    {
        _ = services;
        _ = options;
    }

    public bool CanRun => tools.CanRun;
    public string AuthoringDescription => """
        - "csharp": one C# file per concern under "files" as "<name>.cs" (a single synapse is fine).
                  A synapse is a file with top-level statements and is one reaction: one `await foreach`
                  over one `brain.On<T>(source, brain.Stopping)`, acting through neurons. Shared code is a
                  file of only type declarations. "app.cs" is the app's declaration; never write it.
                  The app's own operations arrive as `AppRequested` signals (change only the reaction):
                    using IntoChat.OS.Modules.Apps;
                    using IntoChat.OS.Modules.Apps.Signals;
                    using IntoChat.OS.Modules.Flutter.Text;

                    await using var brain = await IntoChatOSClient.ConnectAsync(args);
                    var app = brain.Get<IApp>(brain.Setting("App")!);
                    var answer = brain.Get<IText>(app.GetPrimaryKeyString() + "/answer");
                    if ((await answer.Read()).Version == 0) { await answer.Set("Ready. Ask a question."); }
                    await foreach (var request in brain.On<AppRequested>(app, brain.Stopping))
                    { await answer.Set(Answer(request.Input)); }

                    static string Answer(string input) => /* the answer */ input;
                  Never answer a request: a result is neuron state the caller reads.
                  Return entry {"kind":"text","name":"answer"} with these files so the workspace discovers
                  the result. Startup initializes only an unwritten root; it preserves answers on restart.
                  Composition never opens or focuses a workspace entry and discovery is not a readiness claim.
                  Settings are read with brain.Setting("Name").
        """;


    public async Task<ScriptContractCatalog> ReadContracts(IReadOnlyList<string> modules, CancellationToken cancellationToken)
    {
        var discovery = tools.Discovery ?? throw new InvalidOperationException("This host has no script contract discovery.");
        var catalog = await discovery.Read(modules, cancellationToken);
        return new([.. catalog.Modules.Select(module => new ScriptContractModule(module.Id))],
            catalog.Contracts, catalog.Example, catalog.Truncated);
    }

    public async Task<ScriptContractCatalog> ReadContractDetail(string moduleId, string contractName, string? methodName, CancellationToken cancellationToken)
    {
        var discovery = tools.Discovery ?? throw new InvalidOperationException("This host has no script contract discovery.");
        var catalog = await discovery.ReadDetail(moduleId, contractName, methodName, cancellationToken);
        return new([.. catalog.Modules.Select(module => new ScriptContractModule(module.Id))], catalog.Contracts, catalog.Example, catalog.Truncated);
    }

    public async Task<ScriptCompilationCheck> Compile(IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken)
    {
        var checker = tools.Check ?? throw new InvalidOperationException("This host has no script compiler.");
        var result = checker.Compile(files);
        foreach (var compiled in result.Assemblies.Values) { await assemblies.Put(compiled, cancellationToken); }
        return new ScriptCompilationCheck(result.Success,
            [.. result.Errors.Select(error => new ScriptCompilationDiagnostic(error.File, error.Id, error.Line, error.Message))])
        { Hashes = result.Assemblies.ToDictionary(file => file.Key, file => file.Value.Hash, StringComparer.Ordinal) };
    }

    public Task<Verdict[]> Judge(string brain, IReadOnlyDictionary<string, string> bundle, object signal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(signal);
        return Task.FromResult<Verdict[]>([]);
    }
}
