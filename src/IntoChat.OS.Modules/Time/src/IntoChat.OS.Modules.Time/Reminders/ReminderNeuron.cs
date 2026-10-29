using DigitalBrain;
using IntoChat.OS.Modules.Time.Reminders.Signals;
using IntoChat.OS.Modules.Time.Timers;

namespace IntoChat.OS.Modules.Time.Reminders;

// A plain neuron (06-03): one durable reminder named "tick" on the runtime host, which reactivates the neuron; each tick is a
// turn (Neuron.OnReminder) that publishes ReminderTick. A reminder registered on the retired grain type is not migrated.
[Neuron("reminder")]
internal sealed class ReminderNeuron : DigitalBrain.Neuron, IReminder
{
    private const string Name = "tick";
    // Left by the removed automation job runner; retired when it fires.
    private const string RetiredJob = "job";

    public Task Start(TimeSpan dueTime, TimeSpan period)
    {
        // The host also refuses a period under its configured minimum before writing the registration.
        if (dueTime < TimeSpan.Zero || dueTime > TimerNeuron.MaximumDelay)
        { throw new ArgumentOutOfRangeException(nameof(dueTime)); }
        if (period <= TimeSpan.Zero || period > TimerNeuron.MaximumDelay)
        { throw new ArgumentOutOfRangeException(nameof(period)); }
        return Context.Reminders.Start(Name, dueTime, period);
    }

    public Task Stop() => Context.Reminders.Stop(Name);

    protected override async Task OnReminder(string name, CancellationToken cancellationToken)
    {
        // The id is the os key "{brain}/{key}" callers address, as the grain neuron's was; feature scripts match ticks by it.
        if (name == Name) { await Publish(new ReminderTick($"{Id.Brain.Value}/{Id.Key}", Context.Time.GetUtcNow())); }
        else if (name == RetiredJob) { await Context.Reminders.Stop(name); }
    }
}
