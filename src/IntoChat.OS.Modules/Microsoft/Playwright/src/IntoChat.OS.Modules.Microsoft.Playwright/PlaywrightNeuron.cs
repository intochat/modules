using DigitalBrain;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Microsoft.Playwright;

// A plain neuron since 06-06d, reentrant as the grain was. The browser lives in this activation, so the activation is kept alive
// with an ITimers keep-alive timer (04-18) where the grain delayed its deactivation: five minutes for a managed session that
// was just started, for as long as a desktop attachment lasts, and released when the session ends.
[DigitalBrain.Neuron("playwright"), Reentrant]
internal sealed class PlaywrightNeuron(IBrowserSessionProvider provider) : DigitalBrain.Neuron, IPlaywright
{
    private const string KeepAlive = "playwright.session";
    private static readonly TimeSpan ManagedLifetime = TimeSpan.FromMinutes(5);
    // A periodic keep-alive timer holds the activation until it is stopped.
    private static readonly TimeSpan AttachedRenewal = TimeSpan.FromHours(1);

    private readonly BrowserSessionOwner _owner = new(provider);
    private long _generation;
    private string? _managedAccount;
    private string? _managedSessionId;

    private void AuthorizeManaged()
    {
        if (_managedAccount is not null && CallerContextStamper.Require().AccountId != _managedAccount)
        { throw new UnauthorizedAccessException("The managed browser belongs to another account."); }
    }

    private void HoldFor(TimeSpan due, TimeSpan? period) => Context.Timers.Start(KeepAlive, due, period, keepAlive: true);

    private void Release() => Context.Timers.Stop(KeepAlive);

    public async Task Start(CancellationToken ct = default)
    {
        var caller = CallerContextStamper.Require();
        AuthorizeManaged();
        ct.ThrowIfCancellationRequested();
        if (_owner.Read() is { Ready: true } current && current.SessionId == _managedSessionId) { return; }
        var generation = ++_generation;
        _managedAccount = caller.AccountId;
        var sessionId = Guid.NewGuid().ToString("N");
        _managedSessionId = sessionId;
        HoldFor(ManagedLifetime, null);
        try
        {
            await _owner.Start(caller.AccountId, sessionId, ct);
            if (generation == _generation && _owner.Read().Ready)
            { await Publish(new BrowserReady(sessionId)); }
        }
        catch
        {
            if (generation == _generation)
            {
                Release();
                await Publish(new BrowserUnavailable(sessionId));
            }
            throw;
        }
    }

    public Task Stop(CancellationToken ct = default)
    {
        AuthorizeManaged();
        ct.ThrowIfCancellationRequested();
        return _owner.Read().SessionId is { } id ? Detach(id, ct) : Task.CompletedTask;
    }

    public async Task Attach(BrowserAttachment attachment, CancellationToken ct = default)
    {
        AuthorizeManaged();
        BrowserSessionOwner.Validate(attachment);
        ct.ThrowIfCancellationRequested();
        if (_owner.Read() is { Ready: true } current && current.SessionId == attachment.SessionId) { return; }
        var generation = ++_generation;
        HoldFor(AttachedRenewal, AttachedRenewal);
        try
        {
            await _owner.Attach(attachment, ct);
            if (generation == _generation && _owner.Read().Ready)
            { await Publish(new BrowserReady(attachment.SessionId)); }
        }
        catch
        {
            if (generation == _generation)
            {
                if (!_owner.Read().Connected) { Release(); }
                await Publish(new BrowserUnavailable(attachment.SessionId));
            }
            throw;
        }
    }

    public async Task Detach(string sessionId, CancellationToken ct = default)
    {
        AuthorizeManaged();
        ct.ThrowIfCancellationRequested();
        if (_owner.Read().SessionId != sessionId) { return; }
        ++_generation;
        var detach = _owner.Detach(sessionId, ct);
        Release();
        await Publish(new BrowserUnavailable(sessionId));
        await detach;
    }

    public Task<BrowserObservation> Navigate(string url, CancellationToken ct = default) => Run(() => _owner.Navigate(url, ct));
    public Task<BrowserObservation> Snapshot(CancellationToken ct = default) => Run(() => _owner.Snapshot(ct));
    public Task<BrowserObservation> Click(string selector, CancellationToken ct = default) => Run(() => _owner.Click(selector, ct));
    public Task<BrowserObservation> Fill(string selector, string value, CancellationToken ct = default) => Run(() => _owner.Fill(selector, value, ct));
    public Task<string> Evaluate(string expression, CancellationToken ct = default) => Run(() => _owner.Evaluate(expression, ct));
    public Task<BrowserSession> Read() { AuthorizeManaged(); return Task.FromResult(_owner.Read()); }

    private async Task<T> Run<T>(Func<Task<T>> action)
    {
        AuthorizeManaged();
        var generation = _generation;
        var before = _owner.Read();
        var session = before.SessionId;
        try { return await action(); }
        finally
        {
            if (before.Ready && generation == _generation && session is not null && !_owner.Read().Connected)
            { await Detach(session); }
        }
    }

    protected override async Task OnDeactivate(DeactivationCause cause, CancellationToken cancellationToken)
    {
        var session = _owner.Read().SessionId;
        ++_generation;
        await _owner.DisposeAsync();
        if (session is not null) { await Publish(new BrowserUnavailable(session)); }
    }
}
