using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.ArtCraft;

// One per brain, keyed {brain}/artcraft. Open connects the brain to the app's MCP server (its tools then
// appear in search) and answers where the app's own page is served for the shell's browser.
[NeuronAlias("artcraft.studio"), DefaultNeuronType("artcraft.studio")]
public interface IArtCraftStudio
{
    [DigitalBrain.CallTimeout(120000)] Task<ArtCraftSession> Open(string app, CancellationToken ct = default);
}

// McpKey is the brain's IMcpServer for the app; WebUrl is null when the deployment serves no page for it.
[GenerateSerializer, Alias("artcraft.session")]
public sealed record ArtCraftSession(
    [property: Id(0)] string App,
    [property: Id(1)] string Title,
    [property: Id(2)] string McpKey,
    [property: Id(3)] string? WebUrl);
