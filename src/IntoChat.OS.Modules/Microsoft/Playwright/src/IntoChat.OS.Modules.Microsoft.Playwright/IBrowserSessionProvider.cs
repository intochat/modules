namespace IntoChat.OS.Modules.Microsoft.Playwright;

public interface IBrowserSessionProvider
{
    Task<IBrowserPageSession> StartAsync(string accountId, string sessionId, CancellationToken ct)
        => throw new NotSupportedException("Managed browsing is not configured.");
    Task<IBrowserPageSession> AttachAsync(BrowserAttachment attachment, CancellationToken ct);
    // A page the deployment serves itself (PlaywrightModuleOptions.AppOrigins), exempt from the public-only rule.
    bool IsAppPage(string url) => false;
}
public interface IBrowserPageSession : IAsyncDisposable
{
    bool Connected { get; }
    string? Url { get; }
    string? Title { get; }
    Task<BrowserObservation> NavigateAsync(string url, CancellationToken ct);
    Task<BrowserObservation> SnapshotAsync(CancellationToken ct);
    Task<BrowserObservation> ClickAsync(string selector, CancellationToken ct);
    Task<BrowserObservation> FillAsync(string selector, string value, CancellationToken ct);
    Task<string> EvaluateAsync(string expression, CancellationToken ct);
}
