using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Entries.Signals;

[Signal("ui.entry-control-changed")]
public sealed record EntryControlChanged(string Name) : DigitalBrain.Signal;
