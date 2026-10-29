using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Clock.Signals;

[Signal("ui.clock-changed")]
public sealed record ClockChanged(string Name, int Version) : DigitalBrain.Signal;
