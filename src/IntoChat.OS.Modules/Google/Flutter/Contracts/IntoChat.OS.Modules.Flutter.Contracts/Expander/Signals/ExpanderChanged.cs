using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Expander.Signals;

[Signal("ui.expander-changed")]
public sealed record ExpanderChanged(string Name, bool Expanded) : DigitalBrain.Signal;
