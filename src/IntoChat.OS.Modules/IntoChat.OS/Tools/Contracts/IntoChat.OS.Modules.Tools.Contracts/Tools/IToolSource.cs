namespace IntoChat.OS.Modules.Tools;

// A neuron whose tools are discovered at runtime: an MCP server, a page's agent API. Every instance of a
// type deriving this contract is a source; the assistant finds its live tools through neurons_search and runs one through Run; arguments is a JSON object that
// matches the tool's input schema. Implementations derive their own contract from this one.
// R4-21(6): a plain contract (MCP, Gmail, PageTools) derives this shape; every tool source is a plain neuron since 06-08.
public interface IToolSource
{
    [DigitalBrain.Interleaves] Task<ToolSet> Tools(CancellationToken ct = default);
    [DigitalBrain.CallTimeout(600_000)] Task<ToolResult> Run(string tool, string arguments, CancellationToken ct = default);
}
