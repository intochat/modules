using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Calendar.Signals;

[Signal("ui.calendar-changed")]
public sealed record CalendarChanged(string Name, int Version) : DigitalBrain.Signal;
