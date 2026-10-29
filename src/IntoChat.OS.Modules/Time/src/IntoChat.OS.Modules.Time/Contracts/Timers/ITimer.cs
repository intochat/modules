using DigitalBrain.Contracts;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Time.Timers;

[NeuronAlias("timer")]
[DefaultNeuronType("timer")]
public interface ITimer
{
    // Replace the activation-local schedule. Null period means one shot.
    Task Start(TimeSpan dueTime, TimeSpan? period = null);
    // Stop scheduling. Already published signals are not recalled.
    Task Stop();
}
