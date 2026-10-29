using DigitalBrain;
using IntoChat.OS.Modules.Time.Reminders;
using IntoChat.OS.Modules.Time.Reminders.Signals;
using IntoChat.OS.Modules.Time.Timers.Signals;
using DigitalBrain.Testing;
using ITimer = IntoChat.OS.Modules.Time.Timers.ITimer;

namespace IntoChat.OS.Modules.Time.Tests;

// The plain Time neurons (06-03), one fact body per behaviour. The Unit tier runs each on TestOS (a controlled clock); the
// Integration tier compiles this same file and runs each on TestOS and on SiloHost.Local (real time, the runtime BrainLog), so
// the same fact holds on both runtimes (R4-05). Durations are short so the silo kind runs them in real time.
public sealed class TimeNeuronFacts(TimeFixture fixture) : IClassFixture<TimeFixture>
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(20);

    public static TheoryData<NeuronRuntimeKind> Kinds => TimeFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AOneShotTimerPublishesOneTickNamedByItsKey(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("timer", "once");
        var timer = runtime.Get<ITimer>(id);

        await timer.Start(TimeSpan.FromMilliseconds(100));

        var tick = Assert.Single(await runtime.WaitForSignals<TimerTick>(id, 1, Within, ct));
        Assert.Equal($"{id.Brain.Value}/once", tick.TimerId);
        await runtime.Elapse(TimeSpan.FromMilliseconds(500), ct);
        Assert.Single(await runtime.Signals<TimerTick>(id, ct));
        await timer.Stop();
        await timer.Stop();
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task APeriodicTimerTicksUntilStopped(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("timer", "periodic");
        var timer = runtime.Get<ITimer>(id);

        await timer.Start(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));
        await runtime.WaitForSignals<TimerTick>(id, 3, Within, ct);
        await timer.Stop();
        var stopped = (await runtime.Signals<TimerTick>(id, ct)).Count;
        await runtime.Elapse(TimeSpan.FromMilliseconds(500), ct);

        Assert.Equal(stopped, (await runtime.Signals<TimerTick>(id, ct)).Count);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task StartingAgainReplacesTheSchedule(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("timer", "replaced");
        var timer = runtime.Get<ITimer>(id);

        await timer.Start(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300));
        await timer.Start(TimeSpan.FromMilliseconds(100));
        await runtime.WaitForSignals<TimerTick>(id, 1, Within, ct);
        await runtime.Elapse(TimeSpan.FromSeconds(1), ct);

        Assert.Single(await runtime.Signals<TimerTick>(id, ct));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnInvalidScheduleIsRefusedAndTheWorkingTimerStays(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("timer", "invalid");
        var timer = runtime.Get<ITimer>(id);
        await timer.Start(TimeSpan.FromMilliseconds(200));

        foreach (var due in new[] { TimeSpan.FromTicks(-1), Timeout.InfiniteTimeSpan, TimeSpan.MaxValue })
        { await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => timer.Start(due)); }
        foreach (var period in new[] { TimeSpan.Zero, TimeSpan.FromTicks(-1), TimeSpan.MaxValue })
        { await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => timer.Start(TimeSpan.Zero, period)); }

        Assert.Equal($"{id.Brain.Value}/invalid", Assert.Single(await runtime.WaitForSignals<TimerTick>(id, 1, Within, ct)).TimerId);
    }

    // A timer lives with its activation: a restart loses the schedule, and Stop on the new activation creates nothing.
    [Theory, MemberData(nameof(Kinds))]
    public async Task ARestartLosesTheScheduleAndAnotherStartIsNeeded(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var runtime = await fixture.Start(kind, ct);
        var id = runtime.Id("timer", "restarted");
        await runtime.Get<ITimer>(id).Start(TimeSpan.FromMilliseconds(400));

        await runtime.Restart(ct);
        var timer = runtime.Get<ITimer>(id);
        await timer.Stop();
        await runtime.Elapse(TimeSpan.FromSeconds(1), ct);
        Assert.Empty(await runtime.Signals<TimerTick>(id, ct));

        await timer.Start(TimeSpan.Zero);
        Assert.Equal($"{id.Brain.Value}/restarted", Assert.Single(await runtime.WaitForSignals<TimerTick>(id, 1, Within, ct)).TimerId);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AReminderPublishesTypedTicksUntilStoppedAndStopIsIdempotent(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("reminder", "live");
        var reminder = runtime.Get<IReminder>(id);

        await reminder.Start(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        var ticks = await runtime.WaitForSignals<ReminderTick>(id, 2, Within, ct);
        await reminder.Stop();
        await reminder.Stop();

        Assert.All(ticks, tick => Assert.Equal($"{id.Brain.Value}/live", tick.ReminderId));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnInvalidReminderScheduleIsRefused(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var reminder = runtime.Get<IReminder>(runtime.Id("reminder", "invalid"));

        foreach (var due in new[] { TimeSpan.FromTicks(-1), Timeout.InfiniteTimeSpan, TimeSpan.MaxValue })
        { await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reminder.Start(due, TimeSpan.FromMinutes(1))); }
        foreach (var period in new[] { TimeSpan.Zero, Timeout.InfiniteTimeSpan, TimeSpan.MaxValue })
        { await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reminder.Start(TimeSpan.Zero, period)); }
    }

    // A reminder is durable: it reactivates its neuron after a restart and keeps ticking. Missed ticks are not replayed.
    [Theory, MemberData(nameof(Kinds))]
    public async Task AReminderSurvivesARestart(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var runtime = await fixture.Start(kind, ct);
        var id = runtime.Id("reminder", "durable");
        await runtime.Get<IReminder>(id).Start(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
        var before = (await runtime.WaitForSignals<ReminderTick>(id, 1, Within, ct)).Count;

        await runtime.Restart(ct);

        Assert.True((await runtime.WaitForSignals<ReminderTick>(id, before + 1, Within, ct)).Count > before);
        await runtime.Get<IReminder>(id).Stop();
    }
}
