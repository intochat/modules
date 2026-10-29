using DigitalBrain;
using IntoChat.OS.Modules.Time.Timers.Signals;

namespace IntoChat.OS.Modules.Time.Timers;

// A plain neuron (06-03): the schedule is an activation timer of the runtime host, so it dies with the activation and a new
// activation needs another Start. Each tick is a turn (Neuron.OnReminder) that publishes TimerTick.
[Neuron("timer")]
internal sealed class TimerNeuron : DigitalBrain.Neuron, ITimer
{
    private const string Schedule = "schedule";
    internal static readonly TimeSpan MaximumDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private bool _running;
    private bool _once;

    public Task Start(TimeSpan dueTime, TimeSpan? period = null)
    {
        if (dueTime < TimeSpan.Zero || dueTime > MaximumDelay)
        { throw new ArgumentOutOfRangeException(nameof(dueTime)); }
        if (period is { } interval && (interval <= TimeSpan.Zero || interval > MaximumDelay))
        { throw new ArgumentOutOfRangeException(nameof(period)); }

        // A named timer replaces the previous schedule; its ticks do not interleave with calls.
        // TODO(06-09, R4-14): keep-alive. Runtime host timers do not hold the activation (the Framework neuron used
        // KeepAlive(Infinite)), so an idle activation with a long due time can be collected before it fires.
        Context.Timers.Start(Schedule, dueTime, period);
        _running = true;
        _once = period is null;
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _running = false;
        Context.Timers.Stop(Schedule);
        return Task.CompletedTask;
    }

    protected override async Task OnReminder(string name, CancellationToken cancellationToken)
    {
        if (name != Schedule || !_running || cancellationToken.IsCancellationRequested) { return; }
        if (_once) { await Stop(); }
        // The id is the os key "{brain}/{key}" callers address, as the grain neuron's was; feature scripts match ticks by it.
        await Publish(new TimerTick($"{Id.Brain.Value}/{Id.Key}", Context.Time.GetUtcNow()));
    }
}
