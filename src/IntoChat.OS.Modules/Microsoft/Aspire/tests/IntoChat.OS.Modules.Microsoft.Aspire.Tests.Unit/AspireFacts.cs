using DigitalBrain;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Tests.Unit;

public sealed class AspireFacts
{
    [Fact]
    public void TheDefaultOptionsValidate()
        => new AspireOptions().Validate();

    [Fact]
    public void ARollStepBudgetOfZeroOrLessIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspireOptions { SiloStepBudget = TimeSpan.Zero }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspireOptions { SiloStepBudget = TimeSpan.FromSeconds(-1) }.Validate());
    }

    [Fact]
    public void AClusterSamplingIntervalOfZeroOrLessIsRefused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new AspireOptions { ClusterSampling = TimeSpan.Zero }.Validate());

    [Fact]
    public void ANewCandidateQueueHasNothingLiveAndNothingQueued()
    {
        var state = new CandidateQueueState();

        Assert.Null(state.Live);
        Assert.Empty(state.Queue);
    }

    [Fact]
    public void AQueueStateChangesByCopyingAndNeverMutatesTheOriginal()
    {
        var idle = new CandidateQueueState();

        var queued = idle with { Queue = ["c1", "c2"] };
        var promoted = queued with { Live = "c1", Queue = [.. queued.Queue.Skip(1)] };

        Assert.Empty(idle.Queue);
        Assert.Equal(["c1", "c2"], queued.Queue);
        Assert.Equal("c1", promoted.Live);
        Assert.Equal(["c2"], promoted.Queue);
    }

    [Fact]
    public async Task AspireNeuronRecordsAndQueriesLogsAndTracesOnTestOS()
    {
        await using var os = TestOS.Local();
        os.Register<AspireNeuron>();
        var aspire = os.Get<IAspire>(new NeuronId(new BrainId("local"), "aspire", "aspire"));

        var resources = await aspire.GetResourcesAsync();
        Assert.Contains(resources, r => r.Name == "OperatingSystem");

        await aspire.RecordLogAsync(new AspireLogEntry(DateTimeOffset.UtcNow, "OperatingSystem", "Error", "NullReferenceException in CalculatorApp", "System.NullReferenceException"));
        await aspire.RecordTraceAsync(new AspireTraceSpan("trace-1", "span-1", "OperatingSystem", "calculator.add", "Error", 1250, "DivideByZeroException"));

        var errorLogs = await aspire.GetStructuredLogsAsync("OperatingSystem", "Error");
        Assert.Single(errorLogs);
        var consoleLines = await aspire.GetConsoleLogsAsync("OperatingSystem", 10);
        Assert.Single(consoleLines);
        var errorTraces = await aspire.GetTracesAsync("OperatingSystem", errorsOnly: true);
        Assert.Single(errorTraces);
    }
}
