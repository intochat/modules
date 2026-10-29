using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Text.Signals;

[Signal("ui.text-changed")]
public sealed record TextChanged(string Name) : DigitalBrain.Signal;
