namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

internal sealed class Git(IProcessRunner processes, string repository)
{
    public async Task<string> Run(string workingDirectory, CancellationToken ct, params string[] args)
    {
        // Worktrees nest the repository under .intochat-os; Windows paths overflow without long-path support.
        var result = await processes.RunAsync("git", ["-c", "core.longpaths=true", .. args], workingDirectory, null, ct);
        if (result.ExitCode != 0) { throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {result.Error}"); }
        return result.Output.Trim();
    }

    public Task AddWorktree(string path, string baseSha, CancellationToken ct) => Run(repository, ct, "worktree", "add", "--detach", path, baseSha);
    public Task MoveWorktree(string from, string to, CancellationToken ct) => Run(repository, ct, "worktree", "move", from, to);
    public async Task RemoveWorktree(string path, CancellationToken ct)
    {
        try { await Run(repository, ct, "worktree", "remove", "--force", path); }
        catch (InvalidOperationException) { return; } // the worktree is already gone
    }
    public Task Stage(string worktree, CancellationToken ct) => Run(worktree, ct, "add", "-A");
    public Task Commit(string worktree, string message, CancellationToken ct) => Run(worktree, ct, "commit", "-m", message, "--allow-empty");
    public Task<string> ShortSha(string worktree, CancellationToken ct) => Run(worktree, ct, "rev-parse", "--short=7", "HEAD");
    public async Task<string[]> ChangedFiles(string worktree, string baseSha, CancellationToken ct)
        => (await Run(worktree, ct, "diff", "--name-only", baseSha, "HEAD")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    // Moves `reference` to sha by compare-and-swap, fast-forward only, and returns the commit it held (null when
    // it did not exist). No working tree, index or checked-out branch is touched: the repository is shared.
    public async Task<string?> Advance(string reference, string sha, CancellationToken ct)
    {
        var target = await Run(repository, ct, "rev-parse", "--verify", sha + "^{commit}");
        var current = await Resolve(reference, ct);
        if (current == target) { return current; }
        if (current is not null)
        {
            var ancestor = await processes.RunAsync("git", ["merge-base", "--is-ancestor", current, target], repository, null, ct);
            if (ancestor.ExitCode != 0) { throw new InvalidOperationException($"{reference} is at {current}, which {sha} does not descend from."); }
        }
        await Run(repository, ct, "update-ref", "-m", $"promote {sha}", reference, target, current ?? NoCommit);
        return current;
    }

    // Undoes an Advance whose promotion did not take, only while the reference still holds sha: back to what it
    // held, or to sha's parent (the build the candidate was prepared on) when that is not known.
    public async Task Retreat(string reference, string sha, string? previous, CancellationToken ct)
    {
        var target = await Run(repository, ct, "rev-parse", "--verify", sha + "^{commit}");
        var back = previous ?? await Run(repository, ct, "rev-parse", "--verify", sha + "~1^{commit}");
        await Run(repository, ct, "update-ref", "-m", $"revert promotion of {sha}", reference, back, target);
    }

    public async Task<string?> Resolve(string reference, CancellationToken ct)
    {
        var result = await processes.RunAsync("git", ["rev-parse", "--verify", "--quiet", reference + "^{commit}"], repository, null, ct);
        return result.ExitCode == 0 ? result.Output.Trim() : null;
    }

    // update-ref's old value for "must not exist yet".
    private const string NoCommit = "0000000000000000000000000000000000000000";
}
