using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DigitalBrain.Runtime.Orleans.Hosting;
using AzureSdk = Azure;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Authoring;

public sealed class CSharpScriptSandbox(CSharpToolService tools, IBehaviorAssemblies assemblies, IServiceProvider services, IOptions<CSharpOptions> options) : IScriptSandbox
{
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

    // Rules run in the brain's sandbox session, never here: this host compiles the bundle's shared
    // classes off the grain thread (once per content), stores the library like a behavior, and asks the
    // session for verdicts. No answer in time, or no answer at all, is a rejection.
    public async Task<Verdict[]> Judge(string brain, IReadOnlyDictionary<string, string> bundle, object signal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(signal);
        if (tools.Check is not { } checker)
        {
            return bundle.Values.Any(source => CSharpScriptCheck.IsShared(source) && source.Contains("ILogic", StringComparison.Ordinal))
                ? [Verdict.Reject("no sandbox") with { Rule = RulesName }] : [];
        }
        var key = Key(bundle);
        if (!_shared.TryGetValue(key, out var shared))
        {
            shared = await Task.Run(() => checker.CompileShared(bundle), cancellationToken).ConfigureAwait(false);
            if (shared.Library is { } library) { await assemblies.Put(library, cancellationToken).ConfigureAwait(false); }
            if (_shared.Count >= SharedCapacity) { _shared.Clear(); }
            _shared[key] = shared;
        }
        if (shared.Errors.Count > 0)
        {
            return [Verdict.Reject("the rules do not compile: " + string.Join("; ", shared.Errors.Select(error => $"{error.File}:{error.Line} {error.Message}"))) with { Rule = RulesName }];
        }
        if (!shared.HasRules || shared.Library is not { } compiled) { return []; }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.RuleTimeout);
        try
        {
            var url = BrainRuntimeNeuron.Reachable(await assemblies.ReadUrl(compiled.Hash, RuleUrlLifetime, deadline.Token).ConfigureAwait(false));
            var type = signal.GetType();
            var request = new JudgeRequest(compiled.Hash, url, type.FullName + ", " + type.Assembly.GetName().Name,
                JsonSerializer.SerializeToElement(signal, type, RuntimeProtocol.Json),
                // The host answers inside the budget, so a hung rule is reported (and quarantined) rather than cut off.
                options.Value.RuleTimeout * 0.8);
            var runner = services.GetRequiredService<SessionRunner>();
            JudgeAnswer answer;
            try { answer = await runner.Judge(brain, request, deadline.Token).ConfigureAwait(false); }
            catch (HttpRequestException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
            {
                // Sessions are caches: reaped or not yet started, it is ensured like a script run would and asked once more.
                // Whatever the ensuring or the second asking throws (an Orleans timeout, say) is no answer, never a failed request.
                try
                {
                    await SiloNeurons.Get<IBrainRuntimeSession>(services, BrainRuntimeKeys.For(brain))
                        .Hold(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                    answer = await runner.Judge(brain, request, deadline.Token).ConfigureAwait(false);
                }
                catch (Exception retry) when (!cancellationToken.IsCancellationRequested)
                {
                    Log()?.LogDebug(retry, "Judge for brain {Brain} could not ensure the session or ask again.", brain);
                    return [Verdict.Reject("rule did not answer") with { Rule = RulesName }];
                }
            }
            return [.. answer.Verdicts.Select(verdict => new Verdict(verdict.Allowed, verdict.Reason) { Rule = verdict.Rule })];
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested
            && error is OperationCanceledException or HttpRequestException or JsonException or InvalidOperationException or NotSupportedException or AzureSdk.RequestFailedException)
        {
            Log()?.LogDebug(error, "Judge for brain {Brain} got no answer.", brain);
            return [Verdict.Reject("rule did not answer") with { Rule = RulesName }];
        }
    }

    private ILogger? Log() => services.GetService<ILoggerFactory>()?.CreateLogger<CSharpScriptSandbox>();

    private const string RulesName = "rules";
    private const int SharedCapacity = 256;
    private static readonly TimeSpan RuleUrlLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, SharedLibrary> _shared = new(StringComparer.Ordinal);

    private static string Key(IReadOnlyDictionary<string, string> bundle)
    {
        var text = new StringBuilder();
        foreach (var (path, source) in bundle.OrderBy(file => file.Key, StringComparer.Ordinal))
        { text.Append(path).Append((char)0).Append(source).Append((char)0); }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
