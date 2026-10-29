using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Sheet.Signals;

[Signal("ui.sheet-changed")]
public sealed record SheetChanged(string Name, int Version) : DigitalBrain.Signal;
