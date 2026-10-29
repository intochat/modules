using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Button.Signals;

[Signal("ui.button-clicked")]
public sealed record ButtonClicked(string Name, string Action,
    Guid RequestId = default) : DigitalBrain.Signal;
