using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Button.Signals;

[Signal("ui.button-changed")]
public sealed record ButtonChanged(string Name, int Version) : DigitalBrain.Signal;
