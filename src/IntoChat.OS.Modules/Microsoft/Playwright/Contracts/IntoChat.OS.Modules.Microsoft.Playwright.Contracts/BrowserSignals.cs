namespace IntoChat.OS.Modules.Microsoft.Playwright;

[DigitalBrain.Signal("playwright.ready")]
public sealed record BrowserReady(string SessionId) : DigitalBrain.Signal;

[DigitalBrain.Signal("playwright.unavailable")]
public sealed record BrowserUnavailable(string SessionId) : DigitalBrain.Signal;

public sealed class BrowserNotConnectedException() : InvalidOperationException("The browser is not connected. Open or reconnect the browser and retry.");
