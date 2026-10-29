using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Color.Signals;

[Signal("ui.color-changed")]
public sealed record ColorChanged(string Name, string Hex) : DigitalBrain.Signal;
