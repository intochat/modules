using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.WebBrowser.Signals;

[Signal("ui.browser-navigated")]
public sealed record BrowserNavigated(string Name, string Uri) : DigitalBrain.Signal;

// The browser's driver changed (Configure): a plain state change always says what changed.
[Signal("ui.browser-configured")]
public sealed record BrowserConfigured(string Name, string PlaywrightKey) : DigitalBrain.Signal;
