using IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Tests.Unit;

// The git command lines the candidate pipeline issues, against a recording process runner: no git, no process.
public sealed class GitFacts
{
    private const string Repository = "repo";

    [Fact]
    public async Task EveryCommandRunsWithLongPathsInTheGivenDirectoryAndReturnsTrimmedOutput()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(0, "  abc1234\n", ""));

        var sha = await new Git(runner, Repository).ShortSha("wt", TestContext.Current.CancellationToken);

        Assert.Equal("abc1234", sha);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(("git", "wt"), (call.File, call.WorkingDirectory));
        Assert.Equal(["-c", "core.longpaths=true", "rev-parse", "--short=7", "HEAD"], call.Args);
    }

    [Fact]
    public async Task AFailingCommandThrowsWithItsCommandLineAndStderr()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(128, "", "fatal: nope"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new Git(runner, Repository).Stage("wt", TestContext.Current.CancellationToken));

        Assert.Contains("add -A", error.Message, StringComparison.Ordinal);
        Assert.Contains("fatal: nope", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangedFilesSplitsTheDiffIntoTrimmedNonEmptyPaths()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(0, "a.cs\n b/c.cs \n\n", ""));

        var files = await new Git(runner, Repository).ChangedFiles("wt", "base", TestContext.Current.CancellationToken);

        Assert.Equal(["a.cs", "b/c.cs"], files);
        Assert.Equal(["diff", "--name-only", "base", "HEAD"], runner.Calls[0].Args.Skip(2));
    }

    [Fact]
    public async Task RemovingAWorktreeThatIsAlreadyGoneIsNotAnError()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(1, "", "not a worktree"));

        await new Git(runner, Repository).RemoveWorktree("wt", TestContext.Current.CancellationToken);

        Assert.Equal(Repository, Assert.Single(runner.Calls).WorkingDirectory);
    }

    [Fact]
    public async Task ResolveAnswersNullForAReferenceThatDoesNotExist()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(1, "", ""), new ProcessResult(0, "deadbeef\n", ""));
        var git = new Git(runner, Repository);

        Assert.Null(await git.Resolve("refs/heads/missing", TestContext.Current.CancellationToken));
        Assert.Equal("deadbeef", await git.Resolve("refs/heads/live", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdvanceIsANoOpWhenTheReferenceAlreadyHoldsTheTarget()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(0, "T\n", ""), new ProcessResult(0, "T\n", ""));

        var previous = await new Git(runner, Repository).Advance("refs/live", "T", TestContext.Current.CancellationToken);

        Assert.Equal("T", previous);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task AdvanceRefusesAPromotionThatIsNotAFastForward()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(0, "NEW\n", ""), new ProcessResult(0, "OLD\n", ""), new ProcessResult(1, "", ""));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new Git(runner, Repository).Advance("refs/live", "NEW", TestContext.Current.CancellationToken));

        Assert.Contains("does not descend", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(runner.Calls, call => call.Args.Contains("update-ref"));
    }

    [Fact]
    public async Task AdvanceCreatesAMissingReferenceAgainstTheNullCommit()
    {
        var runner = new RecordingRunner();
        runner.Answer(new ProcessResult(0, "NEW\n", ""), new ProcessResult(1, "", ""), new ProcessResult(0, "", ""));

        var previous = await new Git(runner, Repository).Advance("refs/live", "NEW", TestContext.Current.CancellationToken);

        Assert.Null(previous);
        Assert.Equal(["update-ref", "-m", "promote NEW", "refs/live", "NEW", new string('0', 40)], runner.Calls[^1].Args.Skip(2));
    }

    private sealed record Call(string File, string[] Args, string WorkingDirectory);

    private sealed class RecordingRunner : IProcessRunner
    {
        private readonly Queue<ProcessResult> _answers = new();
        private ProcessResult _last = new(0, "", "");

        public List<Call> Calls { get; } = [];

        public void Answer(params ProcessResult[] results)
        {
            foreach (var result in results) { _answers.Enqueue(result); }
        }

        public Task<ProcessResult> RunAsync(string file, string[] args, string workingDirectory, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
        {
            Calls.Add(new Call(file, args, workingDirectory));
            if (_answers.Count > 0) { _last = _answers.Dequeue(); }
            return Task.FromResult(_last);
        }

        public int Detach(string file, string[] args, string workingDirectory) => throw new NotSupportedException();
    }
}
