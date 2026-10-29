using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Tree.Signals;

[Signal("ui.tree-changed")]
public sealed record TreeChanged(string Name, int Version) : DigitalBrain.Signal;
