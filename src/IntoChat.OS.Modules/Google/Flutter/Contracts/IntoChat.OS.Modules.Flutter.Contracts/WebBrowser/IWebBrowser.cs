using DigitalBrain.Contracts;
using DigitalBrain;
using System.Text.Json.Serialization;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.WebBrowser;

[NeuronAlias("webbrowser"), DefaultNeuronType(UIVocabulary.WebBrowserType)]
public interface IWebBrowser
{
    Task Navigate(string uri, string? title = null);
    Task Configure(string playwrightKey);
    [DigitalBrain.ReadOnlyCall] Task<WebBrowserView> Read();
}

// The shell reads the connection by name.
[JsonConverter(typeof(JsonStringEnumConverter<BrowserConnection>))]
public enum BrowserConnection { Waiting, Attached, Unavailable, Failed }

[ExportableState]
public sealed class WebBrowserState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Uri { get; set; } = "";
    public string Title { get; set; } = "";
    public string? PlaywrightKey { get; set; }
}

public sealed class WebBrowserView
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Uri { get; set; } = "";
    public string Title { get; set; } = "";
    public BrowserConnection Connection { get; set; }
    public string Error { get; set; } = "";
    public string ActualUrl { get; set; } = "";
}

[Signal("ui.browser-observation")]
public sealed record BrowserObservationChanged(string Name) : DigitalBrain.Signal;
