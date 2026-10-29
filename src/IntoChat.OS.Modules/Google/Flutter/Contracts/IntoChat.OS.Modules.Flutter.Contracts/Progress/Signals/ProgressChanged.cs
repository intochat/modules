using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Progress.Signals;

[Signal("ui.progress-changed")]
public sealed record ProgressChanged(string Name, double Value) : DigitalBrain.Signal;
