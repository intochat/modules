using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using IntoChat.OS.Modules.AI.Testing.Signals;

namespace IntoChat.OS.Modules.AI.Testing;

[Neuron("testing.verification", State = "test-runs")]
public sealed class TestVerificationNeuron : Neuron<TestVerificationState>, ITestVerificationNeuron
{
    public async Task<TestExecutionResult> RunTargetedSuiteAsync(TestExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await TestProcessRunner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        var runId = Guid.NewGuid().ToString("N");
        var summary = new TestResultSummary(
            runId,
            request.SuiteName,
            DateTimeOffset.UtcNow,
            result.Success,
            result.Total,
            result.Passed,
            result.Failed,
            result.DurationMs);

        var existingRuns = State?.Runs ?? [];
        var updatedRuns = new List<TestResultSummary>(existingRuns) { summary };

        if (updatedRuns.Count > 50)
        {
            updatedRuns.RemoveRange(0, updatedRuns.Count - 50);
        }

        var next = new TestVerificationState
        {
            SuiteId = string.IsNullOrEmpty(State?.SuiteId) ? request.SuiteName : State.SuiteId,
            Runs = updatedRuns.ToArray()
        };

        var signal = new TestSuiteVerified(request.SuiteName, result.Success, result.Total, result.Passed, result.Failed);
        await Save(next, signal);
        return result;
    }

    public Task<IReadOnlyList<TestResultSummary>> GetHistoricalRunsAsync(string testSuiteId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testSuiteId);
        cancellationToken.ThrowIfCancellationRequested();
        var runs = State?.Runs ?? [];
        var matches = runs
            .Where(r => string.Equals(r.SuiteName, testSuiteId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<TestResultSummary>>(matches);
    }
}
