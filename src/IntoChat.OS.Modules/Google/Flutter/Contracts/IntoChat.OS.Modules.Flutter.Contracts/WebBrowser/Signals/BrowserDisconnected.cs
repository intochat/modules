using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.WebBrowser.Signals;

[Signal("ui.browser-disconnected")]
public sealed record BrowserDisconnected(string Name, string SessionId) : DigitalBrain.Signal;
