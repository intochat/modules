using PlaywrightSdk = Microsoft.Playwright;

namespace IntoChat.OS.Modules.Microsoft.Playwright;

public sealed partial class PlaywrightSessionProvider
{
    private readonly SemaphoreSlim _managedGate = new(1, 1);
    private readonly Dictionary<string, ManagedWorker> _workers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _reaper;
    private bool _stopped;

    public async Task<IBrowserPageSession> StartAsync(string accountId, string sessionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        options.Value.Validate();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        await _managedGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_stopped, this);
            _reaper ??= ReapAsync();
            if (!_workers.TryGetValue(accountId, out var worker))
            {
                if (_workers.Count >= options.Value.MaxManagedAccounts)
                { throw new InvalidOperationException("Managed browser account capacity is full."); }
                var startup = PlaywrightSdk.Playwright.CreateAsync();
                PlaywrightSdk.IPlaywright driver;
                try { driver = await startup.WaitAsync(linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { _ = DisposeLateDriverAsync(startup); throw; }
                try
                {
                    using var cancelled = linked.Token.Register(driver.Dispose);
                    var browser = await driver.Chromium.LaunchAsync(new()
                    {
                        Headless = true,
                        ChromiumSandbox = true,
                        ExecutablePath = options.Value.ChromiumExecutablePath,
                        Timeout = 15_000,
                        Proxy = new() { Server = "http://127.0.0.1:9", Bypass = "<-loopback>" },
                        Args = ["--proxy-bypass-list=<-loopback>", "--force-webrtc-ip-handling-policy=disable_non_proxied_udp", "--disable-quic"],
                    }).WaitAsync(linked.Token).ConfigureAwait(false);
                    linked.Token.ThrowIfCancellationRequested();
                    worker = new(driver, browser);
                    _workers.Add(accountId, worker);
                }
                catch (Exception) when (linked.IsCancellationRequested)
                { driver.Dispose(); throw new OperationCanceledException(linked.Token); }
                catch { driver.Dispose(); throw; }
            }
            if (worker.Sessions.Count >= options.Value.MaxManagedSessionsPerAccount)
            { throw new InvalidOperationException("Managed browser session capacity is full for this account."); }
            PlaywrightSdk.IBrowserContext? context = null;
            PageSession? session = null;
            var registered = false;
            try
            {
                context = await worker.Browser.NewContextAsync(new()
                { AcceptDownloads = false, ServiceWorkers = PlaywrightSdk.ServiceWorkerPolicy.Block }).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                var page = await context.NewPageAsync().ConfigureAwait(false);
                // The context has one authorized page. Popup first requests also pass the context route.
                context.Page += (_, popup) => { if (popup != page) { _ = popup.CloseAsync(); } };
                var ownedContext = context;
                session = new PageSession(worker.Driver, worker.Browser, page, new PlaywrightModuleOptions(),
                    () => registered ? ReleaseAsync(accountId, worker, ownedContext, session!) : ownedContext.CloseAsync());
                await session.InitializeAsync().ConfigureAwait(false);
                await page.GotoAsync($"about:blank#intochat-os-{sessionId}").ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                worker.Sessions.Add(session);
                registered = true;
                return session;
            }
            catch
            {
                try
                {
                    if (session is not null) { await session.DisposeAsync().ConfigureAwait(false); }
                    else if (context is not null) { await context.CloseAsync().ConfigureAwait(false); }
                }
                finally
                {
                    if (worker.Sessions.Count == 0)
                    {
                        _workers.Remove(accountId);
                        await CloseWorkerAsync(worker).ConfigureAwait(false);
                    }
                }
                throw;
            }
        }
        finally { _managedGate.Release(); }
    }

    private async Task ReleaseAsync(string accountId, ManagedWorker worker, PlaywrightSdk.IBrowserContext context, PageSession session)
    {
        // Browser shutdown already closes its contexts; a failed close needs nothing more.
        await Task.WhenAny(context.CloseAsync()).ConfigureAwait(false);
        await _managedGate.WaitAsync().ConfigureAwait(false);
        try
        {
            worker.Sessions.Remove(session);
            if (worker.Sessions.Count == 0 && _workers.TryGetValue(accountId, out var current) && ReferenceEquals(current, worker))
            {
                _workers.Remove(accountId);
                await CloseWorkerAsync(worker).ConfigureAwait(false);
            }
        }
        finally { _managedGate.Release(); }
    }

    private async Task ReapAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                PageSession[] expired;
                await _managedGate.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                try
                {
                    expired = _workers.Values.SelectMany(worker => worker.Sessions)
                        .Where(page => !page.Connected || Environment.TickCount64 - page.LastActivity >= options.Value.ManagedSessionIdleTimeout.TotalMilliseconds)
                        .ToArray();
                }
                finally { _managedGate.Release(); }
                foreach (var page in expired) { await page.DisposeAsync().ConfigureAwait(false); }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    private static async Task CloseWorkerAsync(ManagedWorker worker)
    {
        try { await Task.WhenAny(worker.Browser.CloseAsync()).ConfigureAwait(false); }
        finally { worker.Driver.Dispose(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        if (_reaper is not null) { await _reaper.ConfigureAwait(false); }
        PageSession[] sessions;
        await _managedGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _stopped = true;
            sessions = _workers.Values.SelectMany(worker => worker.Sessions).ToArray();
        }
        finally { _managedGate.Release(); }
        foreach (var session in sessions) { await session.DisposeAsync().ConfigureAwait(false); }
        GC.SuppressFinalize(this);
    }

    private sealed class ManagedWorker(PlaywrightSdk.IPlaywright driver, PlaywrightSdk.IBrowser browser)
    {
        public PlaywrightSdk.IPlaywright Driver { get; } = driver;
        public PlaywrightSdk.IBrowser Browser { get; } = browser;
        public HashSet<PageSession> Sessions { get; } = [];
    }
}
