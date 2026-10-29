using IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Tests;

public sealed class GitFacts : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("aspire-git-").FullName;
    private readonly SystemProcessRunner processes = new();

    private async Task<string> GitAsync(string directory, params string[] args)
    {
        var result = await processes.RunAsync("git", ["-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false", .. args], directory, null, TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.Error);
        return result.Output.Trim();
    }

    private const string Live = "refs/intochat-os/live";

    // A repository with a checked-out branch, an uncommitted edit in its tree, and a candidate commit over it.
    private async Task<(string Repository, string Base, string Candidate)> RepositoryAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(repository);
        await GitAsync(repository, "init", "-b", "main");
        await File.WriteAllTextAsync(Path.Combine(repository, "a.txt"), "a", ct);
        await GitAsync(repository, "add", "-A");
        await GitAsync(repository, "commit", "-m", "base");
        var baseSha = await GitAsync(repository, "rev-parse", "HEAD");
        var candidate = Path.Combine(root, "candidate");
        await GitAsync(repository, "worktree", "add", "--detach", candidate, "HEAD");
        await File.WriteAllTextAsync(Path.Combine(candidate, "b.txt"), "b", ct);
        await GitAsync(candidate, "add", "-A");
        await GitAsync(candidate, "commit", "-m", "candidate");
        await File.WriteAllTextAsync(Path.Combine(repository, "a.txt"), "someone else's edit", ct);
        return (repository, baseSha, await GitAsync(candidate, "rev-parse", "HEAD"));
    }

    // The repository is shared: promotion moves the live ref and nothing a person has checked out.
    [Fact]
    public async Task AdvancingTheLiveRefLeavesTheCheckedOutBranchAndItsTreeAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (repository, baseSha, candidate) = await RepositoryAsync();
        var status = await GitAsync(repository, "status", "--porcelain");

        var previous = await new Git(processes, repository).Advance(Live, candidate[..7], ct);

        Assert.Null(previous);
        Assert.Equal(candidate, await GitAsync(repository, "rev-parse", Live));
        Assert.Equal(baseSha, await GitAsync(repository, "rev-parse", "HEAD"));
        Assert.Equal(status, await GitAsync(repository, "status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(repository, "b.txt")));
    }

    [Fact]
    public async Task TheLiveRefOnlyFastForwards()
    {
        var ct = TestContext.Current.CancellationToken;
        var (repository, baseSha, candidate) = await RepositoryAsync();
        var git = new Git(processes, repository);
        await git.Advance(Live, candidate, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => git.Advance(Live, baseSha, ct));
        Assert.Equal(candidate, await GitAsync(repository, "rev-parse", Live));
    }

    [Fact]
    public async Task RetreatingPutsTheLiveRefBackOnTheCandidatesParent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (repository, baseSha, candidate) = await RepositoryAsync();
        var git = new Git(processes, repository);
        await git.Advance(Live, candidate, ct);

        await git.Retreat(Live, candidate, null, ct);

        Assert.Equal(baseSha, await GitAsync(repository, "rev-parse", Live));
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) { File.SetAttributes(file, FileAttributes.Normal); }
        Directory.Delete(root, recursive: true);
    }
}
