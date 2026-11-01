using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Capabilities;
using DigitalBrain.Testing;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Testing;

[Collection("TestVerification")]
public sealed class TestVerificationServiceFacts
{
    private const string SampleTrxXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun id="11111111-2222-3333-4444-555555555555" name="SampleRun" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Times creation="2026-11-01T00:00:00Z" start="2026-11-01T00:00:00Z" finish="2026-11-01T00:00:01Z" />
          <Results>
            <UnitTestResult testName="SuiteA.Test1" outcome="Passed" duration="00:00:00.0100000" />
            <UnitTestResult testName="SuiteA.Test2" outcome="Passed" duration="00:00:00.0200000" />
            <UnitTestResult testName="SuiteA.Test3" outcome="Failed" duration="00:00:00.0300000">
              <Output>
                <ErrorInfo>
                  <Message>Expected 42 but was 0</Message>
                  <StackTrace>at SuiteA.Test3() in Test3.cs:line 10</StackTrace>
                </ErrorInfo>
              </Output>
            </UnitTestResult>
          </Results>
          <ResultSummary outcome="Failed">
            <Counters total="3" executed="3" passed="2" failed="1" error="0" timeout="0" notExecuted="0" />
          </ResultSummary>
        </TestRun>
        """;

    [Fact]
    public void TrxReportParser_ParsesValidTrxWithPassesAndFailures()
    {
        var result = TrxReportParser.ParseXml(SampleTrxXml, 150);

        Assert.False(result.Success);
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Passed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(150, result.DurationMs);
        Assert.Single(result.Failures);
        Assert.Equal("SuiteA.Test3", result.Failures[0].TestName);
        Assert.Equal("Expected 42 but was 0", result.Failures[0].ErrorMessage);
        Assert.Contains("at SuiteA.Test3()", result.Failures[0].StackTrace);
    }

    [Fact]
    public void TrxReportParser_HandlesMissingFile()
    {
        var result = TrxReportParser.Parse("path/to/missing/file.trx", 50);

        Assert.False(result.Success);
        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Failures);
    }

    [Fact]
    public void TrxReportParser_HandlesMalformedXml()
    {
        var result = TrxReportParser.ParseXml("<not<valid>xml", 50);

        Assert.False(result.Success);
        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Failures);
        Assert.Equal("TrxParser", result.Failures[0].TestName);
    }

    [Fact]
    public void TrxReportParser_FallbackWhenCountersMissing()
    {
        const string xmlWithoutCounters = """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="Test1" outcome="Passed" />
                <UnitTestResult testName="Test2" outcome="Passed" />
              </Results>
            </TestRun>
            """;

        var result = TrxReportParser.ParseXml(xmlWithoutCounters, 80);

        Assert.True(result.Success);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task TestVerificationNeuron_RunsTargetedSuite_AndRecordsHistoryInState()
    {
        var ct = TestContext.Current.CancellationToken;
        TestProcessRunner.OverrideExecutor = (req, token) => Task.FromResult(new TestExecutionResult(true, 5, 5, 0, 0, 100, "5 passed", []));
        try
        {
            var os = TestOS.Local();
            os.Register<TestVerificationNeuron>();

            await using (os)
            {
                var neuron = os.Get<ITestVerificationNeuron>(new NeuronId(new BrainId("local"), "testing.verification", "test-suite-a"));

                var req = new TestExecutionRequest("test-suite-a", "Filter1", 30, "debug");
                var result = await neuron.RunTargetedSuiteAsync(req, ct);

                Assert.NotNull(result);
                Assert.True(result.Success);
                Assert.Equal(5, result.Total);

                var history = await neuron.GetHistoricalRunsAsync("test-suite-a", ct);
                Assert.Single(history);
                Assert.Equal("test-suite-a", history[0].SuiteName);
                Assert.False(string.IsNullOrWhiteSpace(history[0].RunId));
            }
        }
        finally
        {
            TestProcessRunner.OverrideExecutor = null;
        }
    }

    [Fact]
    public async Task TestVerificationNeuron_CapsHistoryAtFiftyRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        TestProcessRunner.OverrideExecutor = (req, token) => Task.FromResult(new TestExecutionResult(true, 1, 1, 0, 0, 10, "ok", []));
        try
        {
            var os = TestOS.Local();
            os.Register<TestVerificationNeuron>();

            await using (os)
            {
                var neuron = os.Get<ITestVerificationNeuron>(new NeuronId(new BrainId("local"), "testing.verification", "capped-suite"));

                for (var i = 0; i < 55; i++)
                {
                    var req = new TestExecutionRequest("capped-suite", null, 10, "debug");
                    await neuron.RunTargetedSuiteAsync(req, ct);
                }

                var history = await neuron.GetHistoricalRunsAsync("capped-suite", ct);
                Assert.Equal(50, history.Count);
            }
        }
        finally
        {
            TestProcessRunner.OverrideExecutor = null;
        }
    }

    [Fact]
    public async Task TestVerificationService_DelegatesToNeuron()
    {
        var ct = TestContext.Current.CancellationToken;
        TestProcessRunner.OverrideExecutor = (req, token) => Task.FromResult(new TestExecutionResult(true, 2, 2, 0, 0, 20, "2 passed", []));
        try
        {
            var os = TestOS.Local();
            os.Register<TestVerificationNeuron>();

            await using (os)
            {
                var service = new TestVerificationService(suiteId =>
                    os.Get<ITestVerificationNeuron>(new NeuronId(new BrainId("local"), "testing.verification", suiteId)));

                var req = new TestExecutionRequest("service-suite", null, 10, "debug");
                var result = await service.RunTargetedSuiteAsync(req, ct);

                Assert.NotNull(result);
                Assert.True(result.Success);

                var history = await service.GetHistoricalRunsAsync("service-suite", ct);
                Assert.Single(history);
                Assert.Equal("service-suite", history[0].SuiteName);
            }
        }
        finally
        {
            TestProcessRunner.OverrideExecutor = null;
        }
    }

    [Fact]
    public void TestProcessRunner_FindRepositoryRoot_LocatesSlnx()
    {
        var repoRoot = TestProcessRunner.FindRepositoryRoot();

        Assert.NotNull(repoRoot);
        Assert.NotEmpty(Directory.EnumerateFiles(repoRoot, "*.slnx"));
    }

    [Fact]
    public void TestProcessRunner_FindExecutable_FindsPrebuiltRunner()
    {
        var repoRoot = TestProcessRunner.FindRepositoryRoot();
        var exe = TestProcessRunner.FindExecutable(repoRoot, "IntoChat.OS.Modules.AI.Tests.Unit", "debug")
            ?? TestProcessRunner.FindExecutable(repoRoot, "IntoChat.OS.Modules.AI.Tests.Unit", "release");

        Assert.NotNull(exe);
        Assert.True(File.Exists(exe));
    }

    [Fact]
    public void TrxReportParser_DetectsFailure_EvenWhenCountersFailedIsZero()
    {
        const string inconsistentTrxXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun id="22222222-3333-4444-5555-666666666666" name="InconsistentRun" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="GhostFailure" outcome="Failed">
                  <Output>
                    <ErrorInfo>
                      <Message>Inconsistent counter test failure</Message>
                      <StackTrace>at GhostFailure()</StackTrace>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
              </Results>
              <ResultSummary outcome="Passed">
                <Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" notExecuted="0" />
              </ResultSummary>
            </TestRun>
            """;

        var result = TrxReportParser.ParseXml(inconsistentTrxXml, 50);

        Assert.False(result.Success);
        Assert.True(result.Failed >= 1);
        Assert.Single(result.Failures);
        Assert.Equal("GhostFailure", result.Failures[0].TestName);
        Assert.Equal("Inconsistent counter test failure", result.Failures[0].ErrorMessage);
    }

    [Fact]
    public void ResolveBrainId_RespectsStampedCallerAndExecutionContext()
    {
        var services = new ServiceCollection()
            .AddSingleton<IExecutionContextProvider, ExecutionContextProvider>()
            .BuildServiceProvider();
        var service = new TestVerificationService(services);

        Assert.Equal(new BrainId("local"), service.ResolveBrainId());

        using (CallerContextStamper.StampScope(CapabilityCallers.Of("usr-1", "acc-1", "tenant-beta")))
        {
            Assert.Equal(new BrainId("tenant-beta"), service.ResolveBrainId());
        }

        Assert.Equal(new BrainId("local"), service.ResolveBrainId());
    }
}
