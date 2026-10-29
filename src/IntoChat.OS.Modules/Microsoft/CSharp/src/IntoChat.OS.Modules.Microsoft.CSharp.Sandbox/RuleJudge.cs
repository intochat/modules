using DigitalBrain;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using IntoChat.OS.Client.Runtime.V1;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// Runs an app's rules (ILogic<T> in its shared classes) against one signal, inside the brain's session:
// each brain's rules load into their own context, so statics never cross brains. Anything that fails
// while loading or running a rule is that rule's rejection; a rule never fails open. Each rule runs on a
// dedicated thread from a small per-session budget (a hung thread cannot be stopped, only fenced in), and a
// rule that misses its budget is quarantined until the bundle changes: it is rejected without running.
public sealed class RuleJudge(BehaviorLoader loader, IOptions<SandboxOptions> options)
{
    private const int Capacity = 256;
    private const string Library = "rules";

    private readonly ConcurrentDictionary<(string Session, string Hash), Lazy<Task<RuleSet>>> _loaded = new();
    private readonly ConcurrentQueue<(string Session, string Hash)> _order = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _threads = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Session, string Hash, string Rule), RunState> _quarantined = new();

    private sealed class RunState
    {
        public const int Running = 0, Finished = 1, Abandoned = 2;
        public int State;
    }
    private int _started;

    public int ThreadsStarted => Volatile.Read(ref _started);

    public async Task<JudgeAnswer> JudgeAsync(string session, JudgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = Type.GetType(request.SignalType, throwOnError: false);
        // An os Signal, or a plain neuron's [Signal] record (06-08: AppRequested is plain).
        if (type is null || !typeof(DigitalBrain.Signal).IsAssignableFrom(type))
        { return new([new(Library, false, $"this sandbox does not know the signal {request.SignalType}")]); }
        var signal = request.Signal.Deserialize(type, RuntimeProtocol.Json)!;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        RuleSet rules;
        try { rules = await Load(session, request).WaitAsync(request.Budget, cancellationToken).ConfigureAwait(false); }
        catch (TimeoutException) { return new([new(Library, false, "rule did not answer (still loading)")]); }
        var verdicts = new List<JudgeVerdict>(rules.Failures);
        foreach (var rule in rules.Matching(signal))
        {
            var name = rule.Type.Name;
            var key = (session, request.Hash, rule.Type.FullName ?? name);
            if (_quarantined.ContainsKey(key)) { verdicts.Add(new(name, false, "rule did not answer (quarantined)")); continue; }
            var remaining = request.Budget - clock.Elapsed;
            var slots = _threads.GetOrAdd(session, _ => new SemaphoreSlim(options.Value.RuleThreads));
            if (remaining <= TimeSpan.Zero || !await slots.WaitAsync(remaining, cancellationToken).ConfigureAwait(false))
            { verdicts.Add(new(name, false, "rule did not answer (busy)")); continue; }
            var done = new TaskCompletionSource<JudgeVerdict>(TaskCreationOptions.RunContinuationsAsynchronously);
            var run = new RunState();
            // The slot is released when the thread ends, not when the caller stops waiting: a hung rule keeps its
            // slot. A rule abandoned while running is quarantined until its own thread ends, whichever happens first;
            // the entry is that run, so another run of the same rule finishing cannot clear it.
            var thread = new Thread(() =>
            {
                try { done.SetResult(RuleSet.Run(rule, signal)); }
                finally
                {
                    slots.Release();
                    if (Interlocked.Exchange(ref run.State, RunState.Finished) == RunState.Abandoned) { _quarantined.TryRemove(new(key, run)); }
                }
            })
            { IsBackground = true, Name = "rule " + name };
            try { thread.Start(); }
            catch { slots.Release(); throw; }
            Interlocked.Increment(ref _started);
            var left = request.Budget - clock.Elapsed;
            try { verdicts.Add(await done.Task.WaitAsync(left > TimeSpan.Zero ? left : TimeSpan.Zero, cancellationToken).ConfigureAwait(false)); }
            catch (Exception abandoned) when (abandoned is TimeoutException or OperationCanceledException)
            {
                _quarantined[key] = run;
                if (Interlocked.Exchange(ref run.State, RunState.Abandoned) == RunState.Finished) { _quarantined.TryRemove(new(key, run)); }
                if (abandoned is OperationCanceledException) { throw; }
                verdicts.Add(new(name, false, "rule did not answer"));
            }
        }
        return new([.. verdicts]);
    }

    // The session stopped: its rule contexts unload and its quarantine is forgotten.
    public void Forget(string session)
    {
        foreach (var key in _loaded.Keys.Where(key => key.Session == session).ToArray())
        {
            if (_loaded.TryRemove(key, out var gone) && gone.Value.IsCompletedSuccessfully) { gone.Value.Result.Unload(); }
        }
        foreach (var key in _quarantined.Keys.Where(key => key.Session == session).ToArray()) { _quarantined.TryRemove(key, out _); }
    }

    private Task<RuleSet> Load(string session, JudgeRequest request)
    {
        var key = (session, request.Hash);
        var lazy = _loaded.GetOrAdd(key, _ => new(() => RuleSet.LoadAsync(loader, session, request, CancellationToken.None)));
        if (lazy.IsValueCreated) { return lazy.Value; }
        var task = lazy.Value;
        _order.Enqueue(key);
        while (_order.Count > Capacity && _order.TryDequeue(out var evicted))
        {
            if (_loaded.TryRemove(evicted, out var gone) && gone.Value.IsCompletedSuccessfully) { gone.Value.Result.Unload(); }
        }
        return task;
    }

    private sealed class RuleSet(BehaviorLoader.BehaviorContext? context, (Type Type, Type Signal, MethodInfo Validate)[] rules, JudgeVerdict[] failures)
    {
        public static async Task<RuleSet> LoadAsync(BehaviorLoader loader, string session, JudgeRequest request, CancellationToken cancellationToken)
        {
            byte[] image;
            try { image = await loader.CachedAsync(request.Hash, request.Assembly, verify: true, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException)
            { return new(null, [], [new(Library, false, "the rules could not be fetched: " + error.Message)]); }
            var context = new BehaviorLoader.BehaviorContext($"rules {session} {request.Hash[..12]}");
            Type[] types;
            var failures = new List<JudgeVerdict>();
            try
            {
                using var stream = new MemoryStream(image);
                var assembly = context.LoadFromStream(stream);
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException partial)
                {
                    types = [.. partial.Types.OfType<Type>()];
                    failures.Add(new(Library, false, "a rule could not load: " + string.Join("; ", partial.LoaderExceptions.OfType<Exception>().Select(error => error.Message))));
                }
            }
            catch (Exception error)
            { return new(context, [], [new(Library, false, "the rules could not load: " + error.Message)]); }
            var rules = types
                .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
                .SelectMany(type => type.GetInterfaces()
                    .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ILogic<>))
                    .Select(contract => (type, contract.GetGenericArguments()[0], contract.GetMethod(nameof(ILogic<Signal>.Validate))!)))
                .ToArray();
            return new(context, rules, [.. failures]);
        }

        public JudgeVerdict[] Failures => failures;

        public IEnumerable<(Type Type, Type Signal, MethodInfo Validate)> Matching(object signal)
            => rules.Where(rule => rule.Signal.IsInstanceOfType(signal));

        public static JudgeVerdict Run((Type Type, Type Signal, MethodInfo Validate) rule, object signal)
        {
            try
            {
                var verdict = (Verdict?)rule.Validate.Invoke(Activator.CreateInstance(rule.Type), [signal])
                    ?? throw new InvalidOperationException("the rule returned no verdict");
                return new(rule.Type.Name, verdict.Allowed, verdict.Reason);
            }
            catch (Exception error)
            {
                var cause = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
                return new(rule.Type.Name, false, "the rule failed: " + cause.Message);
            }
        }

        public void Unload() => context?.Unload();
    }
}
