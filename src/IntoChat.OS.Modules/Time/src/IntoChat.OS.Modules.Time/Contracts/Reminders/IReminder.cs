using DigitalBrain.Contracts;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Time.Reminders;

[NeuronAlias("reminder")]
[DefaultNeuronType("reminder")]
public interface IReminder
{
    // Replace the persistent periodic registration. Missed ticks are not replayed.
    Task Start(TimeSpan dueTime, TimeSpan period);
    // Remove the registration. A previously queued tick may still arrive.
    Task Stop();
}
