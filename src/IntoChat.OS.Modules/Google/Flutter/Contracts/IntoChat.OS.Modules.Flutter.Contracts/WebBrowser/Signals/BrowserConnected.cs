using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.WebBrowser.Signals;

[Signal("ui.browser-connected")]
public sealed record BrowserConnected(string Name, string SessionId) : DigitalBrain.Signal;
