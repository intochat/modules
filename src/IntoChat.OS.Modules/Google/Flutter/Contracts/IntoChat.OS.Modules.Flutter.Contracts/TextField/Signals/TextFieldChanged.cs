using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.TextField.Signals;

[Signal("ui.textfield-changed")]
public sealed record TextFieldChanged(string Name, string Value) : DigitalBrain.Signal;
