using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.Microsoft.Playwright;

// A plain contract since 06-06d. Every call may interleave (the neuron is reentrant): a navigation waits on the page while a
// detach or a second connect reaches the browser.
[NeuronAlias("playwright"), DefaultNeuronType("playwright")]
public interface IPlaywright
{
    Task Start(CancellationToken ct = default);
    Task Stop(CancellationToken ct = default);

    // Connector handoff. Apps consume Read, BrowserReady and BrowserUnavailable instead.
    Task Attach(BrowserAttachment attachment, CancellationToken ct = default);
    Task Detach(string sessionId, CancellationToken ct = default);
    Task<BrowserObservation> Navigate(string url, CancellationToken ct = default);
    Task<BrowserObservation> Snapshot(CancellationToken ct = default);
    Task<BrowserObservation> Click(string selector, CancellationToken ct = default);
    Task<BrowserObservation> Fill(string selector, string value, CancellationToken ct = default);
    // Runs a JavaScript expression in the attached page and returns its awaited value as JSON text.
    // Only pages on a deployment app origin (PlaywrightModuleOptions.AppOrigins) accept it.
    Task<string> Evaluate(string expression, CancellationToken ct = default);
    Task<BrowserSession> Read();
}
