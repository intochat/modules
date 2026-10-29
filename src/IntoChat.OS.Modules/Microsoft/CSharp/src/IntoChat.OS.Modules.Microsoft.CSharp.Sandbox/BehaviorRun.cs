using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// A loaded behavior running its entry point on its own task: exit code 0 on a clean return (or when it
// ends because it was told to stop), 1 on an unhandled exception, whose message is kept for the report.
public sealed class BehaviorRun : IAsyncDisposable
{
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource _stopping = new();
    private readonly RunLog _log;
    private AssemblyLoadContext? _context;
    private MethodInfo? _entry;
    // The framework [App] class of a program that has no entry point (see AppRunner).
    private Type? _app;

    private BehaviorRun(AssignmentEvent assignment, AssemblyLoadContext context, int logLines)
    {
        Assignment = assignment;
        Id = assignment.Behavior;
        Hash = assignment.Hash;
        _context = context;
        _log = new RunLog(logLines);
        Completion = Task.FromResult(0);
    }

    public AssignmentEvent Assignment { get; }
    public string Id { get; }
    public string Hash { get; }
    public Task<int> Completion { get; private set; }
    public string? Error { get; private set; }
    public AssemblyLoadContext? Context => _context;

    public string Tail(int lines) => _log.Tail(lines);

    // A line of the host's own for this run's log (an update that did not happen, say), next to what the behavior wrote.
    public void Note(string line) => _log.Append(line);

    // Set while a reassign waits for this run's in-flight work; cleared if the reassign is given up.
    public bool Draining { get; internal set; }

    public static BehaviorRun Start(SessionConnection session, AssignmentEvent assignment, AssemblyLoadContext context, Assembly assembly, int logLines, ILogger logger)
    {
        var run = new BehaviorRun(assignment, context, logLines);
        if (assembly.EntryPoint is { } entry) { run._entry = entry; }
        else { run._app = AppRunner.AppTypeIn(assembly) ?? throw new InvalidOperationException($"{assignment.Behavior} has no entry point and does not declare exactly one [App] class."); }
        run.Completion = Task.Run(() => run.ExecuteAsync(session, assignment.Settings, logger));
        return run;
    }

    // Reflection must return before awaiting script work: a retained host task must not root MethodInfo.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private object? InvokeEntry()
    {
        if (Interlocked.Exchange(ref _app, null) is { } app) { return AppRunner.RunAsync(app, _stopping.Token); }
        var entry = Interlocked.Exchange(ref _entry, null)!;
        object?[] arguments = entry.GetParameters().Length == 0 ? [] : [Array.Empty<string>()];
        return entry.Invoke(null, arguments);
    }

    private async Task<int> ExecuteAsync(SessionConnection session, Dictionary<string, string> settings, ILogger logger)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["Behavior"] = Id, ["Hash"] = Hash });
        using var ambient = AmbientSession.Enter(session, Id, settings, _stopping.Token, Hash, Assignment.Revision);
        using var output = BehaviorConsole.Capture(line => { _log.Append(line); logger.LogInformation("{Behavior}: {Line}", Id, line); });
        object? result = null;
        try
        {
            result = InvokeEntry();
            if (result is Task<int>) { return await ((Task<int>)result).ConfigureAwait(false); }
            if (result is Task) { await ((Task)result).ConfigureAwait(false); return 0; }
            return result is int code ? code : 0;
        }
        catch (Exception error) when (Unwrap(error) is OperationCanceledException && _stopping.IsCancellationRequested) { return 0; }
        catch (Exception error)
        {
            Error = Unwrap(error).ToString();
            _log.Append(Error);
            logger.LogWarning(Unwrap(error), "Behavior {Behavior} {Hash} failed", Id, Hash);
            return 1;
        }
        finally { result = null; }
    }

    private static Exception Unwrap(Exception error) => error is TargetInvocationException { InnerException: { } inner } ? inner : error;

    public async ValueTask DisposeAsync()
    {
        // A session's disposal and the run's own exit can both dispose it; the first one in does.
        var context = Interlocked.Exchange(ref _context, null);
        if (context is null) { return; }
        try
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            await Task.WhenAny(Completion, Task.Delay(StopGrace)).ConfigureAwait(false);
        }
        finally
        {
            context.Unload();
            context = null;
            _stopping.Dispose();
        }
    }
}
