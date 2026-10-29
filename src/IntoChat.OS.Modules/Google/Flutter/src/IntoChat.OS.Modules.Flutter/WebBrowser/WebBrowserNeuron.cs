using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter.WebBrowser.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IntoChat.OS.Modules.Microsoft.Playwright;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Flutter.WebBrowser;

[Neuron(UIVocabulary.WebBrowserType), DigitalBrain.Reentrant]
internal sealed class WebBrowserNeuron
    : UiNeuron<WebBrowserState>, IWebBrowser, IWebBrowserConnector
{
    private const int ReasonClip = 200;
    // Keeps the activation (and with it the attached session) alive while a page is attached (R4-18, ITimers keep-alive).
    private const string KeepAlive = "session";
    private static readonly TimeSpan KeepAliveEvery = TimeSpan.FromMinutes(5);
    private string? _session;
    private int _port;
    private BrowserConnection _connection;
    private string _error = "";
    private string _actualUrl = "";
    private long _generation;
    private Task? _connecting;
    private Task? _disconnecting;
    private string DriverKey => Snapshot.PlaywrightKey ?? FullKey;
    // The browser is a plain neuron (06-06d); this neuron is still a grain and reaches it through the plain-neuron seam.
    private IPlaywright Driver => SiloNeurons.Get<IPlaywright>(ServiceProvider, DriverKey);

    public async Task Configure(string playwrightKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playwrightKey);
        if (Snapshot.PlaywrightKey == playwrightKey) { return; }
        if (_session is not null && DriverKey != playwrightKey)
        { throw new InvalidOperationException("Disconnect the browser before changing its driver."); }
        var current = Snapshot;
        await Save(new() { Name = current.Name, Version = current.Version, Uri = current.Uri, Title = current.Title, PlaywrightKey = playwrightKey }, new BrowserConfigured(FullKey, playwrightKey));
    }

    public Task Connect(int port, string sessionId)
    {
        if (port is < 1024 or > 65535) { throw new ArgumentOutOfRangeException(nameof(port)); }
        ValidateSession(sessionId);
        if (_session == sessionId && _port == port && _connecting is { } connecting) { return connecting; }
        return _connecting = Attach(port, sessionId);
    }

    private async Task Attach(int port, string sessionId)
    {
        var generation = ++_generation;
        _disconnecting = null;
        var repeated = _session == sessionId && _port == port;
        _session = sessionId;
        _port = port;
        if (!repeated) { await Observe(BrowserConnection.Waiting, ""); }
        Context.Timers.Start(KeepAlive, KeepAliveEvery, KeepAliveEvery, keepAlive: true);
        try
        {
            if (repeated && (await Driver.Read()).Ready) { return; }
            if (generation != _generation) { return; }
            await Driver.Attach(new(port, sessionId));
        }
        catch (Exception error)
        {
            if (generation == _generation)
            {
                _session = null;
                _port = 0;
                await Observe(BrowserConnection.Unavailable, error.Message);
                Context.Timers.Stop(KeepAlive);
                if (generation == _generation)
                { await Announce(new BrowserDisconnected(FullKey, sessionId)); }
            }
            throw;
        }
        finally { if (generation == _generation) { _connecting = null; } }
        if (generation != _generation) { return; }
        await Observe(BrowserConnection.Attached, "");
        await Show(Snapshot.Uri);
        if (generation == _generation)
        { await Announce(new BrowserConnected(FullKey, sessionId)); }
    }

    // The neuron's Uri is what the attached page shows; an empty Uri leaves the page to its driver.
    private async Task Show(string uri)
    {
        if (_session is null) { return; }
        try
        {
            var observed = uri.Length == 0 ? await Driver.Snapshot() : await Driver.Navigate(uri);
            _actualUrl = observed.Url;
            if (_connection is not BrowserConnection.Attached || _error.Length > 0)
            { await Observe(BrowserConnection.Attached, ""); }
        }
        catch (Exception error)
        {
            await Observe(BrowserConnection.Failed,
                uri.Length == 0 ? error.Message : $"Browser could not load {uri}: {error.Message}");
            await Review();
        }
    }

    public async Task Disconnect(string sessionId)
    {
        ValidateSession(sessionId);
        if (_session != sessionId) { return; }
        var disconnecting = _disconnecting ??= Detach(sessionId);
        try { await disconnecting; }
        finally { if (_disconnecting == disconnecting) { _disconnecting = null; } }
    }

    private async Task Detach(string sessionId)
    {
        var generation = ++_generation;
        _connecting = null;
        await Driver.Detach(sessionId);
        if (generation != _generation) { return; }
        _session = null;
        _port = 0;
        await Observe(BrowserConnection.Waiting, "");
        Context.Timers.Stop(KeepAlive);
        if (generation == _generation)
        { await Announce(new BrowserDisconnected(FullKey, sessionId)); }
    }

    public async Task Unavailable(string reason)
    {
        var clipped = (reason ?? "").Length > ReasonClip ? reason![..ReasonClip] : reason ?? "";
        await Observe(BrowserConnection.Unavailable, clipped);
        await Review();
    }

    private async Task Observe(BrowserConnection connection, string error)
    {
        if (_connection == connection && _error == error) { return; }
        _connection = connection;
        _error = error;
        if (connection is BrowserConnection.Waiting or BrowserConnection.Unavailable) { _actualUrl = ""; }
    }

    private async Task Review() =>
        await Dispatch(UiEvent.Of(new BrowserObservationChanged(FullKey)));

    private async Task Announce(DigitalBrain.Signal signal)
    {
        await Publish(signal);
        await Dispatch(UiEvent.Of(signal));
    }

    protected override async Task OnDeactivate(DigitalBrain.DeactivationCause cause, CancellationToken cancellationToken)
    {
        if (_session is { } session)
        {
            try { await Driver.Detach(session, cancellationToken).WaitAsync(cancellationToken); }
            catch (Exception error)
            {
                // Cleanup is best effort: the driver or silo may already be stopping.
                ServiceProvider.GetRequiredService<ILogger<WebBrowserNeuron>>().LogSwallowed(error, "detaching the driver session while deactivating");
            }
        }
    }

    private static void ValidateSession(string sessionId)
    {
        if (sessionId is null || !Regex.IsMatch(sessionId, "\\A[a-f0-9]{32}\\z"))
        { throw new ArgumentException("A browser session must be 32 lowercase hexadecimal characters.", nameof(sessionId)); }
    }

    public async Task Navigate(string uri, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Navigate requires an absolute http(s) URI.", nameof(uri));
        }

        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Uri = parsed.AbsoluteUri;
        next.Title = string.IsNullOrWhiteSpace(title) ? parsed.Host : title.Trim();
        var navigated = new BrowserNavigated(FullKey, next.Uri);
        await Save(next, navigated);
        if (_session is not null) { await Show(next.Uri); }
        await Dispatch(UiEvent.Of(navigated));
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<WebBrowserView> Read() => Task.FromResult(new WebBrowserView
    {
        Name = FullKey,
        Version = Snapshot.Version,
        Uri = Snapshot.Uri,
        Title = Snapshot.Title,
        Connection = _connection,
        Error = _error,
        ActualUrl = _actualUrl,
    });
}
