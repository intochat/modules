using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using PlaywrightSdk = Microsoft.Playwright;

namespace IntoChat.OS.Modules.Microsoft.Playwright.Tests.Browser;

// A deployment app page (loopback, declared in AppOrigins) loads directly and accepts Evaluate;
// everything else keeps the public-only research browser rules.
public sealed class AppPageFacts
{
    private const string Page = "<!doctype html><title>App</title><script>window.app = { answer: async (n) => ({ doubled: n * 2 }) };</script>";

    [Fact]
    public async Task EvaluateAwaitsTheAppPagesApiAndReturnsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await AppServer.StartAsync(ct);
        await using var browser = await DebuggableBrowser.LaunchAsync(ct);
        await using var owner = new BrowserSessionOwner(Provider(app.Origin));
        await owner.Attach(new(browser.Port, browser.SessionId), ct);

        await owner.Navigate(app.Origin + "/", ct);

        Assert.Equal("{\"doubled\":42}", await owner.Evaluate("window.app.answer(21)", ct));
    }

    [Fact]
    public async Task EvaluateIsRefusedOffTheAppOrigins()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var browser = await DebuggableBrowser.LaunchAsync(ct);
        await using var owner = new BrowserSessionOwner(Provider("http://127.0.0.1:1"));
        await owner.Attach(new(browser.Port, browser.SessionId), ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.Evaluate("1 + 1", ct));
    }

    [Fact]
    public async Task ALoopbackPageOutsideTheAppOriginsStaysPrivate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await AppServer.StartAsync(ct);
        await using var browser = await DebuggableBrowser.LaunchAsync(ct);
        await using var owner = new BrowserSessionOwner(Provider("http://127.0.0.1:1"));
        await owner.Attach(new(browser.Port, browser.SessionId), ct);

        await Assert.ThrowsAsync<ArgumentException>(() => owner.Navigate(app.Origin + "/", ct));
    }

    private static PlaywrightSessionProvider Provider(string origin)
        => new(Options.Create(new PlaywrightModuleOptions { AppOrigins = [origin] }));

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class AppServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serving;
        public string Origin { get; }

        private AppServer(string origin)
        {
            Origin = origin;
            _listener.Prefixes.Add(origin + "/");
            _listener.Start();
            _serving = ServeAsync();
        }

        public static Task<AppServer> StartAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new AppServer($"http://127.0.0.1:{FreePort()}"));
        }

        private async Task ServeAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync().WaitAsync(_stop.Token); }
                catch (Exception) when (_stop.IsCancellationRequested) { return; }
                var body = System.Text.Encoding.UTF8.GetBytes(Page);
                context.Response.ContentType = "text/html";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Close();
            await _serving;
            _stop.Dispose();
        }
    }

    private sealed class DebuggableBrowser(PlaywrightSdk.IPlaywright driver, PlaywrightSdk.IBrowser browser, int port, string sessionId) : IAsyncDisposable
    {
        public int Port => port;
        public string SessionId => sessionId;

        public static async Task<DebuggableBrowser> LaunchAsync(CancellationToken ct)
        {
            var (port, sessionId) = (FreePort(), Guid.NewGuid().ToString("N"));
            var driver = await PlaywrightSdk.Playwright.CreateAsync().WaitAsync(ct);
            var browser = await driver.Chromium.LaunchAsync(new() { Args = [$"--remote-debugging-port={port}"] }).WaitAsync(ct);
            var page = await browser.NewPageAsync().WaitAsync(ct);
            await page.GotoAsync($"about:blank#intochat-os-{sessionId}").WaitAsync(ct);
            return new(driver, browser, port, sessionId);
        }

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            driver.Dispose();
        }
    }
}
