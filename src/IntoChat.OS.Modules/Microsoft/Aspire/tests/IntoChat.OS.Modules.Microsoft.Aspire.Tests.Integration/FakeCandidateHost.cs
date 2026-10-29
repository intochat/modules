using IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Tests;

internal sealed class FakeCandidateHost : ICandidateHost
{
    private int prepared;

    public List<string> Calls { get; } = [];
    public string? FailAt { get; set; }
    public bool Healthy { get; set; } = true;
    public string[] Orphaned { get; set; } = [];
    public Exception? RunError { get; set; }
    public string[] OrphansKnown { get; private set; } = [];
    public string? PreparedFrom { get; private set; }
    public Dictionary<string, string> RunEnvironment { get; } = [];

    public Task<PreparedCandidate> Prepare(string baseSha, string summary, IReadOnlyDictionary<string, string> files, CancellationToken ct)
    {
        Calls.Add("prepare");
        PreparedFrom = baseSha;
        Fail("prepare");
        var sha = $"sha{++prepared:0000}";
        return Task.FromResult(new PreparedCandidate(sha, $"worktrees/{sha}"));
    }

    public Task BuildGate(string sha, CancellationToken ct)
    {
        Calls.Add($"gate {sha}");
        Fail("gate");
        return Task.CompletedTask;
    }

    public Task<string> Publish(string sha, CancellationToken ct)
    {
        Calls.Add($"publish {sha}");
        Fail("publish");
        return Task.FromResult($"builds/{sha}");
    }

    public Task<RunningCandidate> Run(string sha, CancellationToken ct)
    {
        Calls.Add($"run {sha}");
        Fail("run");
        if (RunError is { } error)
        {
            RunError = null;
            throw error;
        }
        return Task.FromResult(new RunningCandidate(sha, new CandidateEndpoints("http://localhost:5191", "http://localhost:5190"), $"candidate-{sha}"));
    }

    public Task WaitHealthy(string sha, TimeSpan budget, CancellationToken ct)
    {
        Calls.Add($"healthy {sha}");
        return Healthy ? Task.CompletedTask : Task.FromException(new TimeoutException($"{sha} not healthy"));
    }

    public string? Live { get; private set; } = "base000";

    public Task<string?> Commit(string sha, CancellationToken ct)
    {
        Calls.Add($"commit {sha}");
        Fail("commit");
        var previous = Live;
        Live = sha;
        return Task.FromResult(previous);
    }

    public Task Uncommit(string sha, string? previous, CancellationToken ct)
    {
        Calls.Add($"uncommit {sha} {previous}");
        if (Live == sha) { Live = previous ?? "parent"; }
        return Task.CompletedTask;
    }

    public bool RemoveFails { get; set; }

    public Task Remove(string sha, CancellationToken ct)
    {
        Calls.Add($"remove {sha}");
        return RemoveFails ? Task.FromException(new CandidateHostException("remove", "docker gone")) : Task.CompletedTask;
    }

    public Task<string[]> Orphans(string[] known, CancellationToken ct)
    {
        Calls.Add("orphans");
        OrphansKnown = known;
        Fail("orphans");
        return Task.FromResult(Orphaned);
    }

    private void Fail(string stage)
    {
        if (FailAt == stage)
        {
            throw new CandidateHostException(stage, $"{stage} failed");
        }
    }
}
