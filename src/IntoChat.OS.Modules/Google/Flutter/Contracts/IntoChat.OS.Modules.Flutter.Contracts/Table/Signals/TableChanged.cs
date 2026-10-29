using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Table.Signals;

[Signal("ui.table-changed")]
public sealed record TableChanged(string Name, int Version) : DigitalBrain.Signal;
