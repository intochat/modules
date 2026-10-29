using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using PlaywrightSdk = Microsoft.Playwright;

using MicrosoftSdk = Microsoft;
namespace IntoChat.OS.Modules.Microsoft.Playwright;

// Connects to a page owned by the desktop. It never creates or closes native pages.
public sealed partial class PlaywrightSessionProvider(MicrosoftSdk.Extensions.Options.IOptions<PlaywrightModuleOptions> options) : IBrowserSessionProvider, IAsyncDisposable
{
    public bool IsAppPage(string url) => options.Value.IsAppPage(url);

    public PlaywrightSessionProvider() : this(MicrosoftSdk.Extensions.Options.Options.Create(new PlaywrightModuleOptions())) { }

    private readonly ConcurrentDictionary<(int Port, string SessionId), string> _targets = new();

    internal static T SelectTarget<T>(IEnumerable<T> pages, Func<T, string> url, string sessionId)
    {
        var matches = pages.Where(page => url(page) == $"about:blank#intochat-os-{sessionId}").ToArray();
        if (matches.Length != 1) { throw new InvalidOperationException("The uniquely marked visible browser page was not found."); }
        return matches[0];
    }

    private static async Task<string> TargetIdAsync(PlaywrightSdk.IPage page)
    {
        var cdp = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        try
        {
            var result = await cdp.SendAsync("Target.getTargetInfo").ConfigureAwait(false);
            return result!.Value.GetProperty("targetInfo").GetProperty("targetId").GetString()!;
        }
        finally { await cdp.DetachAsync().ConfigureAwait(false); }
    }

    private async Task<PlaywrightSdk.IPage> SelectPageAsync(PlaywrightSdk.IBrowser browser, BrowserAttachment attachment)
    {
        var pages = browser.Contexts.SelectMany(context => context.Pages).ToArray();
        var key = (attachment.Port, attachment.SessionId);
        var targets = new Dictionary<string, PlaywrightSdk.IPage>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            targets[await TargetIdAsync(page).ConfigureAwait(false)] = page;
        }
        foreach (var entry in _targets)
        {
            // Native target existence, not elapsed wall time, determines whether a session can reconnect.
            if (entry.Key.Port == attachment.Port && !targets.ContainsKey(entry.Value))
            {
                _targets.TryRemove(entry.Key, out _);
            }
        }
        PlaywrightSdk.IPage? selected = null;
        if (_targets.TryGetValue(key, out var known))
        {
            targets.TryGetValue(known, out selected);
            if (selected is null)
            {
                _targets.TryRemove(key, out _);
                throw new InvalidOperationException("The previously attached visible page has closed.");
            }
        }
        else
        {
            selected = SelectTarget(pages, page => page.Url, attachment.SessionId);
        }
        if (_targets.Count >= 256 && !_targets.ContainsKey(key)) { throw new InvalidOperationException("Too many browser sessions."); }
        _targets[key] = await TargetIdAsync(selected).ConfigureAwait(false);
        return selected;
    }
    public async Task<IBrowserPageSession> AttachAsync(BrowserAttachment attachment, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var startup = PlaywrightSdk.Playwright.CreateAsync();
        PlaywrightSdk.IPlaywright playwright;
        try { playwright = await startup.WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            _ = DisposeLateDriverAsync(startup);
            throw;
        }
        // Disposing the driver interrupts startup/actions without Browser.close on the native owner.
        using var cancelled = ct.Register(playwright.Dispose);
        try
        {
            ct.ThrowIfCancellationRequested();
            var browser = await playwright.Chromium.ConnectOverCDPAsync(
                $"http://127.0.0.1:{attachment.Port}", new() { Timeout = 15_000 }).WaitAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var page = await SelectPageAsync(browser, attachment).WaitAsync(ct).ConfigureAwait(false);
            var session = new PageSession(playwright, browser, page, options.Value);
            try
            {
                await session.InitializeAsync().WaitAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                return session;
            }
            catch { await session.DisposeAsync().ConfigureAwait(false); throw; }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            playwright.Dispose();
            throw new OperationCanceledException(ct);
        }
        catch { playwright.Dispose(); throw; }
    }

    private static async Task DisposeLateDriverAsync(Task<PlaywrightSdk.IPlaywright> startup)
    {
        // CreateAsync has no cancellation API. Own and clean any driver returned after cancellation.
        // The caller already received cancellation; cleanup consumes late startup errors.
        await Task.WhenAny(startup).ConfigureAwait(false);
        if (startup.IsCompletedSuccessfully) { startup.Result.Dispose(); }
    }

    private sealed class PageSession(PlaywrightSdk.IPlaywright driver, PlaywrightSdk.IBrowser browser, PlaywrightSdk.IPage page, PlaywrightModuleOptions apps, Func<Task>? release = null) : IBrowserPageSession
    {
        private readonly CancellationTokenSource _lifetime = new();
        private readonly HttpClient _http = new(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = PublicBrowserNetwork.ConnectPublicAsync,
        })
        { Timeout = TimeSpan.FromSeconds(20) };
        private int _disposed;
        private Task? _closing;
        private readonly object _closeLock = new();
        public long LastActivity { get; private set; } = Environment.TickCount64;
        private BrowserActivityBudget _budget = new();
        private string? _title;
        public bool Connected => Volatile.Read(ref _disposed) == 0 && browser.IsConnected && !page.IsClosed;
        public string? Url => Connected ? page.Url : null;
        public string? Title => Connected ? _title : null;

        internal async Task InitializeAsync()
        {
            page.SetDefaultTimeout(15_000);
            page.SetDefaultNavigationTimeout(20_000);
            if (release is null)
            {
                await page.RouteAsync("**/*", RouteAsync).ConfigureAwait(false);
                await page.RouteWebSocketAsync("**/*", socket => socket.CloseAsync()).ConfigureAwait(false);
            }
            else
            {
                await page.Context.RouteAsync("**/*", RouteAsync).ConfigureAwait(false);
                await page.Context.RouteWebSocketAsync("**/*", socket => socket.CloseAsync()).ConfigureAwait(false);
            }
        }

        public Task<BrowserObservation> NavigateAsync(string url, CancellationToken ct) => ExecuteAsync(async () =>
        {
            _budget = new BrowserActivityBudget();
            if (apps.IsAppPage(url))
            {
                await page.GotoAsync(url, new() { WaitUntil = PlaywrightSdk.WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
                return;
            }
            var uri = PublicBrowserNetwork.PublicUri(url);
            await PublicBrowserNetwork.ResolvePublicAsync(uri.IdnHost, ct).ConfigureAwait(false);
            // Resolve HTTP redirects before navigation, so the actual visible page URL remains truthful.
            uri = await ResolveNavigationAsync(uri, ct).ConfigureAwait(false);
            await page.GotoAsync(uri.AbsoluteUri, new() { WaitUntil = PlaywrightSdk.WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        }, ct);

        public Task<BrowserObservation> SnapshotAsync(CancellationToken ct) => ExecuteAsync(() => Task.CompletedTask, ct);
        public Task<BrowserObservation> ClickAsync(string selector, CancellationToken ct)
        {
            ValidateSelector(selector);
            return ExecuteAsync(() => { _budget = new BrowserActivityBudget(); return page.Locator(selector).ClickAsync(); }, ct);
        }
        public Task<BrowserObservation> FillAsync(string selector, string value, CancellationToken ct)
        {
            ValidateSelector(selector);
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 16_000) { throw new ArgumentException("Input is too long.", nameof(value)); }
            return ExecuteAsync(() => { _budget = new BrowserActivityBudget(); return page.Locator(selector).FillAsync(value); }, ct);
        }
        public async Task<string> EvaluateAsync(string expression, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            if (expression.Length > 64_000) { throw new ArgumentException("Expression is too long.", nameof(expression)); }
            if (!Connected) { throw new InvalidOperationException("The visible browser is disconnected."); }
            if (!apps.IsAppPage(page.Url)) { throw new InvalidOperationException("Evaluate runs only on the deployment's app pages."); }
            LastActivity = Environment.TickCount64;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            // The page serializes its own awaited value: the text is exactly what the app's API returned.
            var json = await page.EvaluateAsync<string>(
                "async () => { const value = await (" + expression + "\n); return value === undefined ? 'null' : JSON.stringify(value); }")
                .WaitAsync(linked.Token).ConfigureAwait(false);
            return json.Length <= 256 * 1024 ? json : throw new InvalidOperationException("The evaluated result exceeds 256 KB.");
        }

        private static void ValidateSelector(string selector)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selector);
            if (selector.Length > 2048) { throw new ArgumentException("Selector is too long.", nameof(selector)); }
        }

        private async Task<BrowserObservation> ExecuteAsync(Func<Task> action, CancellationToken ct)
        {
            LastActivity = Environment.TickCount64;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            linked.Token.ThrowIfCancellationRequested();
            using var cancelled = linked.Token.Register(Disconnect);
            try
            {
                if (!Connected) { throw new InvalidOperationException("The visible browser is disconnected."); }
                await action().WaitAsync(linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                // about:blank is allowed only before the first public navigation.
                if (!page.Url.StartsWith("about:blank#intochat-os-", StringComparison.Ordinal) && !apps.IsAppPage(page.Url))
                { PublicBrowserNetwork.PublicUri(page.Url); }
                var snapshot = await page.EvaluateAsync<JsonElement>("""
                    () => {
                        const links = [];
                        const seen = new Set();
                        for (const a of document.querySelectorAll('a[href]')) {
                            const url = a.href;
                            if (url.length > 2048 || !/^(https?:|mailto:)/i.test(url) || seen.has(url)) continue;
                            seen.add(url);
                            links.push({text: (a.innerText || a.getAttribute('aria-label') || '').trim().slice(0,160), url});
                            if (links.length === 80) break;
                        }
                        return {url: location.href, title: document.title.slice(0,300),
                            text: (document.body?.innerText || '').trim().slice(0,16000), links};
                    }
                    """).WaitAsync(linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                _title = snapshot.GetProperty("title").GetString()!;
                return new(snapshot.GetProperty("url").GetString()!, _title,
                    snapshot.GetProperty("text").GetString()!,
                    snapshot.GetProperty("links").EnumerateArray().Select(link =>
                        new BrowserLink(link.GetProperty("text").GetString()!, link.GetProperty("url").GetString()!)).ToArray());
            }
            catch (Exception) when (linked.IsCancellationRequested)
            {
                // WaitAsync may complete before the token's earlier disconnect callback runs.
                // Establish disconnection before exposing completion to a subsequent retry.
                Disconnect();
                throw new OperationCanceledException(linked.Token);
            }
        }

        private async Task<Uri> ResolveNavigationAsync(Uri uri, CancellationToken ct)
        {
            for (var redirect = 0; redirect < 9; redirect++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) { return uri; }
                uri = RedirectUri(uri, response);
            }
            throw new InvalidOperationException("The website exceeded eight redirects.");
        }

        private static Uri RedirectUri(Uri source, HttpResponseMessage response) =>
            PublicBrowserNetwork.PublicUri(new Uri(source, response.Headers.Location
                ?? throw new InvalidOperationException("The redirect has no location.")).AbsoluteUri);

        private async Task RouteAsync(PlaywrightSdk.IRoute route)
        {
            if (apps.IsAppPage(route.Request.Url) && (route.Request.IsNavigationRequest || apps.IsAppPage(page.Url)))
            {
                // The deployment's own app talking to its own origin: no public proxy, no budget. The shell's
                // WebView sends loopback to a dead proxy, so the driver fetches the request and fulfils it.
                try
                {
                    var response = await route.FetchAsync(new() { MaxRedirects = 0 }).ConfigureAwait(false);
                    await route.FulfillAsync(new() { Response = response }).ConfigureAwait(false);
                }
                catch (PlaywrightSdk.PlaywrightException)
                {
                    await Task.WhenAny(route.AbortAsync("connectionfailed")).ConfigureAwait(false);
                }
                return;
            }
            var budget = _budget;
            try
            {
                if (route.Request.Method is not ("GET" or "HEAD") || !budget.TryRequest())
                { throw new InvalidOperationException("Only bounded read-only browsing is supported."); }
                var uri = PublicBrowserNetwork.PublicUri(route.Request.Url);
                for (var redirects = 0; redirects < 9; redirects++)
                {
                    using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), uri);
                    foreach (var header in route.Request.Headers)
                    {
                        if (header.Key is "accept" or "accept-language" or "user-agent")
                        { request.Headers.TryAddWithoutValidation(header.Key, header.Value); }
                    }
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _lifetime.Token).ConfigureAwait(false);
                    if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                    {
                        uri = RedirectUri(uri, response);
                        await PublicBrowserNetwork.ResolvePublicAsync(uri.IdnHost, _lifetime.Token).ConfigureAwait(false);
                        if (route.Request.IsNavigationRequest)
                        {
                            // Do not pass native 3xx responses through: Chromium can follow them outside routing.
                            // A normal Navigate resolves redirects first. Click/server-dependent redirects expose a
                            // truthful intermediate page with a validated link for the next visible navigation.
                            var url = WebUtility.HtmlEncode(uri.AbsoluteUri);
                            await route.FulfillAsync(new()
                            {
                                Status = 200,
                                ContentType = "text/html",
                                Body = $"<!doctype html><title>Continue to website</title><p>This website redirects to <a href=\"{url}\">{url}</a>.</p>"
                            }).ConfigureAwait(false);
                            return;
                        }
                        continue;
                    }
                    if (response.Content.Headers.ContentLength > 8 * 1024 * 1024
                        || response.Content.Headers.ContentDisposition?.DispositionType.Equals("attachment", StringComparison.OrdinalIgnoreCase) == true)
                    { throw new InvalidOperationException("Downloads and oversized resources are disabled."); }
                    await using var stream = await response.Content.ReadAsStreamAsync(_lifetime.Token).ConfigureAwait(false);
                    using var body = new MemoryStream();
                    var buffer = new byte[16 * 1024];
                    int count;
                    while ((count = await stream.ReadAsync(buffer, _lifetime.Token).ConfigureAwait(false)) > 0)
                    {
                        if (body.Length + count > 8 * 1024 * 1024 || !budget.TryReceive(count))
                        { throw new InvalidOperationException("Browser response limit exceeded."); }
                        body.Write(buffer, 0, count);
                    }
                    var headers = response.Headers.Concat(response.Content.Headers)
                        .Where(header => !new[] { "content-length", "content-encoding", "transfer-encoding", "set-cookie", "refresh" }
                            .Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                        .ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
                    await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = body.ToArray() }).ConfigureAwait(false);
                    return;
                }
                throw new InvalidOperationException("Redirect limit exceeded.");
            }
            catch (Exception error) when (error is HttpRequestException or IOException or System.Net.Sockets.SocketException or ArgumentException
                or InvalidOperationException or OperationCanceledException or PlaywrightSdk.PlaywrightException)
            {
                await Task.WhenAny(route.AbortAsync("blockedbyclient")).ConfigureAwait(false);
            }
        }

        private void Disconnect()
        {
            lock (_closeLock)
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
                _lifetime.Cancel();
                // Desktop pages belong to Flutter; managed pages close only their own context.
                if (release is null) { driver.Dispose(); }
                else { _closing = release(); }
            }
        }

        public async ValueTask DisposeAsync()
        {
            Disconnect();
            _http.Dispose();
            GC.SuppressFinalize(this);
            Task? closing;
            lock (_closeLock) { closing = _closing; }
            if (closing is not null) { await closing.ConfigureAwait(false); }
        }
    }
}
