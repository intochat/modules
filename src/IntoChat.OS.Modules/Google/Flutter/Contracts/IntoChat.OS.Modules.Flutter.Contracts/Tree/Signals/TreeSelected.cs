using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Tree.Signals;

[Signal("ui.tree-selected")]
public sealed record TreeSelected(string Name, string SelectedId) : DigitalBrain.Signal;
