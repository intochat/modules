namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

internal sealed record PreparedCandidate(string Sha, string Worktree);
internal sealed record RunningCandidate(string Sha, CandidateEndpoints Endpoints, string Container);

// The only code that touches git, dotnet, docker and Azurite for candidates.
internal interface ICandidateHost
{
    Task<PreparedCandidate> Prepare(string baseSha, string summary, IReadOnlyDictionary<string, string> files, CancellationToken ct);
    Task BuildGate(string sha, CancellationToken ct);                 // dotnet build + touched tests; throws CandidateHostException
    Task<string> Publish(string sha, CancellationToken ct);           // returns build folder {Workdir}/builds/{sha}
    Task<RunningCandidate> Run(string sha, CancellationToken ct);     // image + isolated fresh storage
    Task WaitHealthy(string sha, TimeSpan budget, CancellationToken ct); // throws TimeoutException
    Task<string?> Commit(string sha, CancellationToken ct);            // CAS fast-forward of refs/intochat-os/live to sha; returns what it held
    Task Uncommit(string sha, string? previous, CancellationToken ct); // puts the live ref back while it still holds sha
    Task Remove(string sha, CancellationToken ct);                    // container + its azurite + network
    Task<string[]> Orphans(string[] known, CancellationToken ct);     // removes candidate-* containers not in known, returns removed
}

internal sealed class CandidateHostException(string stage, string message) : Exception(message)
{
    public string Stage { get; } = stage;
}
