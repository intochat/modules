using DigitalBrain.Contracts;
using IntoChat.OS.Modules.Tools;

namespace IntoChat.OS.Modules.Microsoft.Playwright;

// A deployment app page's agent API (window.<Api>, e.g. window.filmcraft) as a tool source, keyed
// {brain}/pages/{name}. It runs through the IPlaywright neuron at Driver, which must be attached to the
// page; the page answers only while it renders, so Run reports a hidden page instead of waiting on it.
// A plain contract and a plain tool source since 06-06d.
[NeuronAlias("playwright.page-tools"), DefaultNeuronType("playwright.page-tools")]
public interface IPageTools : IToolSource
{
    Task<PageToolsState> Configure(PageToolsSpec spec, CancellationToken ct = default);
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<PageToolsState> Read();
}

public sealed record PageToolsSpec(string Title, string Driver, string Api);

public sealed record PageToolsState(string Title, string Driver, string Api, bool Configured);

// The page tools after a Configure (06-06d: a plain save publishes what changed).
[DigitalBrain.Signal("playwright.page-tools-changed")]
public sealed record PageToolsChanged(PageToolsState State) : DigitalBrain.Signal;
