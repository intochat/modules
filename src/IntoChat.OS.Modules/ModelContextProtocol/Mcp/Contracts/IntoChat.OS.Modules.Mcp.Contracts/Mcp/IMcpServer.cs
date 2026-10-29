using DigitalBrain.Contracts;
using IntoChat.OS.Modules.Tools;

namespace IntoChat.OS.Modules.Mcp;

// One MCP server reached over Streamable HTTP, keyed {brain}/mcp/{name}. Connect lists its tools and
// registers them with the brain's tool sources; Run calls one. Credentials never live here.
// A plain contract since 06-06d: a tool source (IToolSource) the kernel lists through its instance directory.
[NeuronAlias("mcp.server"), DefaultNeuronType("mcp.server")]
public interface IMcpServer : IToolSource
{
    [DigitalBrain.CallTimeout(120_000)] Task<McpServerState> Connect(McpServerSpec spec, CancellationToken ct = default);
    Task Disconnect(CancellationToken ct = default);
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<McpServerState> Read();
}

public sealed record McpServerSpec(string Title, string Url);

public sealed record McpServerState(
    string Title,
    string Url,
    bool Connected,
    ToolDescriptor[] Tools,
    string? Error,
    DateTimeOffset? RefreshedAt);

[DigitalBrain.Signal("mcp.tool-ran")]
public sealed record McpToolRan(string Tool, bool IsError, int ElapsedMs) : DigitalBrain.Signal;

// The server's state after a connect or disconnect (06-06d: a plain save publishes what changed).
[DigitalBrain.Signal("mcp.server-changed")]
public sealed record McpServerChanged(McpServerState State) : DigitalBrain.Signal;
