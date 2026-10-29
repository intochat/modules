using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Time.Reminders.Signals;

[Signal("time.reminder-tick")]
public sealed record ReminderTick(
    string ReminderId,
    DateTimeOffset ObservedAt) : DigitalBrain.Signal;
