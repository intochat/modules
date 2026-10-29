using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Graph.Signals;

[Signal("ui.graph-changed")]
public sealed record GraphChanged(string Name, int Version) : DigitalBrain.Signal;
