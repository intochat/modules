namespace IntoChat.OS.Modules.Microsoft.Aspire.Deployment;

// The live AppHost, reached through the `aspire` CLI.
internal interface IClusterHost
{
    Task<string> CurrentSha(CancellationToken ct);                        // from {Workdir}/live.json
    Task SetLive(string sha, CancellationToken ct);                       // writes live.json (build must exist)
    Task RestartSilos(CancellationToken ct);                              // `aspire resource ... restart`: intochat-os-mcp-*, then OperatingSystem-* (the calling silo last)
    Task RestartInstance(string name, CancellationToken ct);              // `aspire resource {name} restart`: one instance, by name from Instances(); the calling silo's own restart is detached
    Task DrainInstance(string name, CancellationToken ct);                // POST /drain on the instance's own endpoint: its /ready turns Unhealthy, it keeps serving
    Task WaitOutOfFront(string name, TimeSpan budget, CancellationToken ct); // the front no longer routes name and still routes another instance of its role; else InstanceNeverReadyException
    Task WaitInstance(string name, DateTimeOffset since, TimeSpan budget, CancellationToken ct); // Running and the front routes it (/_instances/{name}/ready answers 200); else InstanceNeverReadyException (budget spent, or a process started after since exited)
    Task<string[]> Instances(CancellationToken ct);                       // the roll order: intochat-os-mcp-{x}, then OperatingSystem-{x}
    string? Self { get; }                                                 // the OperatingSystem-{x} instance this process is
    Task<int> Spans(string service, string span, CancellationToken ct);  // `aspire otel spans`: spans named `span` whose source is `service` or `service-*`
}

// The one never-ready outcome a roll step reacts to: the instance outlived its budget without becoming ready, or stopped.
internal sealed class InstanceNeverReadyException(string message, Exception? inner = null) : Exception(message, inner);
