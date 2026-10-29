using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Time.Timers.Signals;

[Signal("time.timer-tick")]
public sealed record TimerTick(
    string TimerId,
    DateTimeOffset ObservedAt) : DigitalBrain.Signal;
