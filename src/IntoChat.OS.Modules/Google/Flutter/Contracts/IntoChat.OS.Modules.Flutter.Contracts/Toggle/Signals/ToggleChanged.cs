using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Toggle.Signals;

[Signal("ui.toggle-changed")]
public sealed record ToggleChanged(string Name, bool On) : DigitalBrain.Signal;
