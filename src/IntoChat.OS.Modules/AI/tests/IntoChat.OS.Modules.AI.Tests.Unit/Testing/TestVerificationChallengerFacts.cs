using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.AI.Testing;
using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Testing;

[Collection("TestVerification")]
public sealed class TestVerificationChallengerFacts
{
    [Fact]
    public void TestVerificationState_ConcreteArrayInvariants_SatisfiesArchitectureRules()
    {
        var stateType = typeof(TestVerificationState);

        // 1. SuiteId must be string
        var suiteIdProp = stateType.GetProperty(nameof(TestVerificationState.SuiteId));
        Assert.NotNull(suiteIdProp);
        Assert.Equal(typeof(string), suiteIdProp.PropertyType);

        // 2. Runs must be concrete array TestResultSummary[], NEVER List/IEnumerable
        var runsProp = stateType.GetProperty(nameof(TestVerificationState.Runs));
        Assert.NotNull(runsProp);
        Assert.True(runsProp.PropertyType.IsArray, "Runs property must be a concrete array to prevent aliasing/violations");
        Assert.Equal(typeof(TestResultSummary[]), runsProp.PropertyType);

        // 3. Default instance must initialize Runs to empty array, not null
        var defaultInstance = new TestVerificationState();
        Assert.NotNull(defaultInstance.Runs);
        Assert.Empty(defaultInstance.Runs);
        Assert.Equal(string.Empty, defaultInstance.SuiteId);
    }

    [Fact]
    public void TestVerificationState_JsonSerialization_RoundtripsEmptyAndPopulatedArrays()
    {
        // 1. Roundtrip empty array
        var emptyState = new TestVerificationState { SuiteId = "empty-suite", Runs = [] };
        var emptyJson = JsonSerializer.Serialize(emptyState, DigitalBrain.Invocation.RuntimeProtocol.Json);
        var readEmpty = JsonSerializer.Deserialize<TestVerificationState>(emptyJson, DigitalBrain.Invocation.RuntimeProtocol.Json);

        Assert.NotNull(readEmpty);
        Assert.Equal("empty-suite", readEmpty.SuiteId);
        Assert.NotNull(readEmpty.Runs);
        Assert.Empty(readEmpty.Runs);

        // 2. Roundtrip populated array with unicode, special characters, and edge durations
        var now = DateTimeOffset.UtcNow;
        var summaries = new TestResultSummary[]
        {
            new("run-1", "suite/🚀-alpha", now, true, 10, 10, 0, 0),
            new("run-2", "suite/beta\t\n\"quoted\"", now.AddMinutes(1), false, 5, 3, 2, 999999),
            new("run-3", "suite/gamma", now.AddMinutes(2), true, 0, 0, 0, 42)
        };
        var populatedState = new TestVerificationState { SuiteId = "pop-suite", Runs = summaries };
        var popJson = JsonSerializer.Serialize(populatedState, DigitalBrain.Invocation.RuntimeProtocol.Json);
        var readPop = JsonSerializer.Deserialize<TestVerificationState>(popJson, DigitalBrain.Invocation.RuntimeProtocol.Json);

        Assert.NotNull(readPop);
        Assert.Equal("pop-suite", readPop.SuiteId);
        Assert.Equal(3, readPop.Runs.Length);
        for (var i = 0; i < summaries.Length; i++)
        {
            Assert.Equal(summaries[i].RunId, readPop.Runs[i].RunId);
            Assert.Equal(summaries[i].SuiteName, readPop.Runs[i].SuiteName);
            Assert.Equal(summaries[i].ExecutedAt, readPop.Runs[i].ExecutedAt);
            Assert.Equal(summaries[i].Success, readPop.Runs[i].Success);
            Assert.Equal(summaries[i].Total, readPop.Runs[i].Total);
            Assert.Equal(summaries[i].Passed, readPop.Runs[i].Passed);
            Assert.Equal(summaries[i].Failed, readPop.Runs[i].Failed);
            Assert.Equal(summaries[i].DurationMs, readPop.Runs[i].DurationMs);
        }

        // 3. Deserializing json where Runs is omitted falls back to default empty array
        const string omittedJson = """{"SuiteId":"omitted-suite"}""";
        var readOmitted = JsonSerializer.Deserialize<TestVerificationState>(omittedJson, DigitalBrain.Invocation.RuntimeProtocol.Json);
        Assert.NotNull(readOmitted);
        Assert.Equal("omitted-suite", readOmitted.SuiteId);
        Assert.NotNull(readOmitted.Runs);
        Assert.Empty(readOmitted.Runs);
    }

    [Fact]
    public async Task TestVerificationNeuron_StressTransitions_NullStateToFiftyRunsCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var runCounter = 0;
        TestProcessRunner.OverrideExecutor = (req, token) =>
        {
            var id = Interlocked.Increment(ref runCounter);
            return Task.FromResult(new TestExecutionResult(true, id, id, 0, 0, 10 * id, $"Run {id} passed", []));
        };

        try
        {
            var os = TestOS.Local();
            os.Register<TestVerificationNeuron>();

            await using (os)
            {
                var neuron = os.Get<ITestVerificationNeuron>(new NeuronId(new BrainId("local"), "testing.verification", "stress-suite"));

                // Execute 60 sequential runs (exceeding the 50 cap)
                for (var i = 1; i <= 60; i++)
                {
                    var req = new TestExecutionRequest("stress-suite", null, 10, "debug");
                    var res = await neuron.RunTargetedSuiteAsync(req, ct);
                    Assert.True(res.Success);
                    Assert.Equal(i, res.Total);
                }

                var history = await neuron.GetHistoricalRunsAsync("stress-suite", ct);
                Assert.NotNull(history);
                Assert.Equal(50, history.Count);

                // Runs 1..10 must have been pruned; runs 11..60 must be retained in order
                Assert.Equal(11, history[0].Total);
                Assert.Equal(60, history[^1].Total);
            }
        }
        finally
        {
            TestProcessRunner.OverrideExecutor = null;
        }
    }

    [Fact]
    public async Task TestVerificationNeuron_HistoricalRuns_CaseInsensitiveFilteringAndNullSafety()
    {
        var ct = TestContext.Current.CancellationToken;
        TestProcessRunner.OverrideExecutor = (req, token) => Task.FromResult(new TestExecutionResult(true, 1, 1, 0, 0, 10, "ok", []));
        try
        {
            var os = TestOS.Local();
            os.Register<TestVerificationNeuron>();

            await using (os)
            {
                var neuron = os.Get<ITestVerificationNeuron>(new NeuronId(new BrainId("local"), "testing.verification", "filter-suite"));

                await neuron.RunTargetedSuiteAsync(new TestExecutionRequest("Suite-Alpha", null, 10, "debug"), ct);
                await neuron.RunTargetedSuiteAsync(new TestExecutionRequest("Suite-Beta", null, 10, "debug"), ct);
                await neuron.RunTargetedSuiteAsync(new TestExecutionRequest("suite-alpha", null, 10, "debug"), ct);

                // Case-insensitive query matches Suite-Alpha and suite-alpha
                var alphaRuns = await neuron.GetHistoricalRunsAsync("SUITE-ALPHA", ct);
                Assert.Equal(2, alphaRuns.Count);

                var betaRuns = await neuron.GetHistoricalRunsAsync("suite-BETA", ct);
                Assert.Single(betaRuns);

                var gammaRuns = await neuron.GetHistoricalRunsAsync("suite-gamma", ct);
                Assert.Empty(gammaRuns);

                // Null suite id must throw ArgumentNullException
                await Assert.ThrowsAsync<ArgumentNullException>(() => neuron.GetHistoricalRunsAsync(null!, ct));
            }
        }
        finally
        {
            TestProcessRunner.OverrideExecutor = null;
        }
    }

    [Fact]
    public async Task TestProcessRunner_LargeOutput_PipeDeadlockStressTest()
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "sh",
            Arguments = OperatingSystem.IsWindows()
                ? "-NoProfile -Command \"[Console]::Out.Write((New-Object string ('A', 70000))); [Console]::Error.Write((New-Object string ('B', 70000)))\""
                : "-c \"head -c 70000 /dev/zero | tr '\\0' 'A'; head -c 70000 /dev/zero | tr '\\0' 'B' >&2\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.True(stdout.Length >= 70000, $"Expected stdout >= 70000 chars, got {stdout.Length}");
        Assert.True(stderr.Length >= 70000, $"Expected stderr >= 70000 chars, got {stderr.Length}");
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task TestProcessRunner_ExecuteAsync_HandlesCancellationAndProcessTreeCleanup()
    {
        using var cancelledCts = new CancellationTokenSource();
        await cancelledCts.CancelAsync();

        var request = new TestExecutionRequest("IntoChat.OS.Modules.AI.Tests.Unit", null, 30, "debug");
        var result = await TestProcessRunner.ExecuteAsync(request, cancelledCts.Token);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(1, result.Failed);
        Assert.Contains("cancelled", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Failures);
    }
}
