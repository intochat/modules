using IntoChat.OS.Modules.Microsoft.CSharp.Files;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

public enum SessionStatus { None, Starting, Connected, Refused }

public sealed record BrainRuntimeSnapshot(
    SessionStatus Session,
    string? SessionId,
    string? Fingerprint,
    DateTimeOffset? LastHeartbeat,
    IReadOnlyList<BehaviorView> Behaviors,
    bool IsReady = false,
    // This activation's ready persistent scripts and queued pricing have settled; other runtimes may still work.
    bool IsIdle = false);

// Runs counts the behavior's finished runs, succeeded or failed, Failures the failed ones, and
// LastDurationMicroseconds is the last success's time: what this runtime activation saw of this Hash, so a
// reactivation or a rewrite starts again.
public sealed record BehaviorView(
    string Id,
    string Hash,
    string Status,
    string? Error,
    CSharpSubscriptionView[] Subscriptions,
    long Runs = 0,
    long Failures = 0,
    long LastDurationMicroseconds = 0);
