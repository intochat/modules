using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Map.Signals;

[Signal("ui.map-changed")]
public sealed record MapChanged(string Name, int Version) : DigitalBrain.Signal;
