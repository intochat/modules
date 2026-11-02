using System.Security.Cryptography;
using System.Text;
using DigitalBrain.Capabilities;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Programs.Signals;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Programs;

// A dedicated per-app virtual actor ({brain}/app/{name}) managing compilation, assembly SHA,
// settings, execution status, and subscriber cursors with isolated actor turns.
[DigitalBrain.Neuron("microsoft.csharp.app-program")]
internal sealed class AppProgramNeuron : DigitalBrain.Neuron<AppProgramState>, IAppProgram
{
    private readonly CSharpScriptCheck? _compiler;
    private readonly IBehaviorAssemblies? _assemblies;

    public AppProgramNeuron() : this(null, null) { }

    public AppProgramNeuron(CSharpScriptCheck? compiler, IBehaviorAssemblies? assemblies)
    {
        _compiler = compiler;
        _assemblies = assemblies;
    }

    private CSharpScriptCheck Compiler => _compiler ?? Context.Services.GetService<CSharpScriptCheck>() ?? new CSharpScriptCheck();
    private IBehaviorAssemblies Assemblies => _assemblies ?? Context.Services.GetService<IBehaviorAssemblies>() ?? new MemoryBehaviorAssemblies();

    private string Brain => Id.Brain.Value;

    private string AppName
    {
        get
        {
            if (Id.Key.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
            {
                return Id.Key[4..];
            }
            if (Id.Key.StartsWith("apps/", StringComparison.OrdinalIgnoreCase))
            {
                return Id.Key[5..];
            }
            return Id.Key;
        }
    }

    private string AppKey => AppProgramKeys.For(Brain, AppName);

    public async Task<ProgramView> Assign(Dictionary<string, string> sources, Dictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(settings);
        if (sources.Count == 0)
        {
            throw new ArgumentException("Pass at least one source file to assign.", nameof(sources));
        }

        var compiler = Compiler;
        var assemblies = Assemblies;

        var compiled = await Task.Run(() => compiler.Compile(sources)).ConfigureAwait(false);
        if (!compiled.Success)
        {
            throw new InvalidOperationException("The program does not compile: "
                + string.Join("; ", compiled.Errors.Select(e => $"{e.File} line {e.Line} {e.Id}: {e.Message}")));
        }

        foreach (var behavior in compiled.Assemblies.Values)
        {
            if (!await assemblies.Exists(behavior.Hash, CancellationToken.None).ConfigureAwait(false))
            {
                await assemblies.Put(behavior, CancellationToken.None).ConfigureAwait(false);
            }
        }

        BehaviorAssembly[] behaviorAssemblies = [.. compiled.Assemblies.OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => new BehaviorAssembly(f.Key, f.Value.Hash))];
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", behaviorAssemblies.Select(b => b.Path + ":" + b.Hash)))));
        var caller = CallerContextStamper.TryGet(out var stamped) ? stamped : State.Principal ?? CapabilityCallers.Of(Brain, Brain, Brain);

        var next = State with
        {
            AppName = AppName,
            AppKey = AppKey,
            CompiledSha = hash,
            Principal = caller,
            SettingEntries = StringEntry.From(settings),
            Behaviors = behaviorAssemblies,
            Status = AppProgramStatus.Assigned,
            Error = null,
        };

        await Save(next, new AppProgramChanged(AppKey)).ConfigureAwait(false);
        return ToView();
    }

    public async Task<ProgramView> AssignPrebuilt(Dictionary<string, PrebuiltBehavior> behaviors, Dictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(behaviors);
        ArgumentNullException.ThrowIfNull(settings);
        if (behaviors.Count == 0)
        {
            throw new ArgumentException("Pass at least one behavior to assign.", nameof(behaviors));
        }
        if (behaviors.Values.Any(b => b?.Assembly is null || b.Symbols is null || b.Hash is null))
        {
            throw new ArgumentException("A prebuilt behavior carries its assembly, its symbols and its hash.", nameof(behaviors));
        }

        var assemblies = Assemblies;

        foreach (var (path, behavior) in behaviors)
        {
            if (behavior.Assembly.Length == 0)
            {
                if (!await assemblies.Exists(behavior.Hash, CancellationToken.None).ConfigureAwait(false))
                {
                    throw new InvalidOperationException($"The assembly for {path} ({behavior.Hash}) was not staged; stage it before assigning.");
                }
                continue;
            }

            var computed = Convert.ToHexStringLower(SHA256.HashData(behavior.Assembly));
            if (!string.Equals(computed, behavior.Hash, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The prebuilt assembly for {path} does not hash to {behavior.Hash}; refusing to assign it.");
            }

            if (!await assemblies.Exists(behavior.Hash, CancellationToken.None).ConfigureAwait(false))
            {
                await assemblies.Put(new CompiledBehavior(behavior.Assembly, behavior.Symbols, behavior.Hash), CancellationToken.None).ConfigureAwait(false);
            }
        }

        BehaviorAssembly[] behaviorAssemblies = [.. behaviors.OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => new BehaviorAssembly(f.Key, f.Value.Hash))];
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", behaviorAssemblies.Select(b => b.Path + ":" + b.Hash)))));
        var caller = CallerContextStamper.TryGet(out var stamped) ? stamped : State.Principal ?? CapabilityCallers.Of(Brain, Brain, Brain);

        var next = State with
        {
            AppName = AppName,
            AppKey = AppKey,
            CompiledSha = hash,
            Principal = caller,
            SettingEntries = StringEntry.From(settings),
            Behaviors = behaviorAssemblies,
            Status = AppProgramStatus.Assigned,
            Error = null,
        };

        await Save(next, new AppProgramChanged(AppKey)).ConfigureAwait(false);
        return ToView();
    }

    public async Task Retire()
    {
        if (State.Status == AppProgramStatus.Retired)
        {
            return;
        }

        var next = State with
        {
            Status = AppProgramStatus.Retired,
        };

        await Save(next, new AppProgramChanged(AppKey)).ConfigureAwait(false);
    }

    public Task<ProgramView?> Describe()
    {
        if (State.Status == AppProgramStatus.Drafted && string.IsNullOrEmpty(State.CompiledSha))
        {
            return Task.FromResult<ProgramView?>(null);
        }

        return Task.FromResult<ProgramView?>(ToView());
    }

    public async Task Acknowledge(long watermark, long cpuMicroseconds, long wallMicroseconds)
    {
        var next = State with
        {
            SignalCursor = Math.Max(State.SignalCursor, watermark),
            Watermark = Math.Max(State.Watermark, watermark),
            CpuMicroseconds = State.CpuMicroseconds + cpuMicroseconds,
            WallMicroseconds = State.WallMicroseconds + wallMicroseconds,
            LastAcknowledgedAt = Context.Time.GetUtcNow(),
        };

        await Save(next, new AppProgramChanged(AppKey)).ConfigureAwait(false);
    }

    public Task<string> Logs(int tail)
    {
        if (string.IsNullOrEmpty(State.Logs))
        {
            return Task.FromResult(string.Empty);
        }

        if (tail <= 0)
        {
            return Task.FromResult(State.Logs);
        }

        var lines = State.Logs.Split('\n');
        if (lines.Length <= tail)
        {
            return Task.FromResult(State.Logs);
        }

        return Task.FromResult(string.Join("\n", lines.TakeLast(tail)));
    }

    private ProgramView ToView()
    {
        var views = State.Behaviors.Select(b => new BehaviorView(
            Id: b.Id(AppKey),
            Hash: b.Hash,
            Status: State.Status == AppProgramStatus.Assigned ? "Running" : "Stopped",
            Error: State.Error,
            Subscriptions: [],
            Runs: 0,
            Failures: 0,
            LastDurationMicroseconds: 0)).ToArray();

        return new ProgramView(AppKey, State.CompiledSha, views);
    }
}
