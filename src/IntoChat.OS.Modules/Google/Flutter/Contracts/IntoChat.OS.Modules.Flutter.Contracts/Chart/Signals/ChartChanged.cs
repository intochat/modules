using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Chart.Signals;

[Signal("ui.chart-changed")]
public sealed record ChartChanged(string Name, int Version, string Title) : DigitalBrain.Signal;
