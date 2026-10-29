using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Tabs.Signals;

[Signal("ui.tabs-changed")]
public sealed record TabsChanged(string Name, string SelectedId) : DigitalBrain.Signal;
