using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.InfoBar.Signals;

[Signal("ui.infobar-dismissed")]
public sealed record InfoBarDismissed(string Name) : DigitalBrain.Signal;
