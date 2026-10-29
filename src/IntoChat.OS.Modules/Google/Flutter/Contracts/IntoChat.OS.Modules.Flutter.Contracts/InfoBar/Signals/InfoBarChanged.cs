using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.InfoBar.Signals;

[Signal("ui.infobar-changed")]
public sealed record InfoBarChanged(string Name, bool Visible) : DigitalBrain.Signal;
