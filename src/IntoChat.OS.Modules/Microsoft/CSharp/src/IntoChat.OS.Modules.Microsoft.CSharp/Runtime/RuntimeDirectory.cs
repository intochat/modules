using System.Collections.Concurrent;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// The speaking session and the assigned programs, published after the runtime's state is durable.
// An invocation reads it instead of asking the runtime grain. A miss, a refused session, or a session
// this process has not published yet falls back to the grain, which remains the authority.
internal sealed class RuntimeDirectory
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Publish(string runtimeKey, BrainRuntimeState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeKey);
        ArgumentNullException.ThrowIfNull(state);
        var behaviors = new Dictionary<string, BehaviorIdentity>(StringComparer.Ordinal);
        foreach (var program in state.Programs.OrderBy(program => program.Owner.Length))
        {
            var identity = new BehaviorIdentity(program.Principal, program.Owner);
            foreach (var behavior in program.Behaviors) { behaviors[behavior.Id(program.Owner)] = identity; }
        }
        _entries[runtimeKey] = new(state.Session?.Id, state.Session?.Status, behaviors);
    }

    public bool TryIdentity(string runtimeKey, string session, string behavior, out BehaviorIdentity? identity)
    {
        identity = null;
        if (!_entries.TryGetValue(runtimeKey, out var entry) || entry.SessionId != session || entry.Status is null or SessionStatus.Refused)
        { return false; }
        return entry.Behaviors.TryGetValue(behavior, out identity);
    }

    private sealed record Entry(string? SessionId, SessionStatus? Status, Dictionary<string, BehaviorIdentity> Behaviors);
}
