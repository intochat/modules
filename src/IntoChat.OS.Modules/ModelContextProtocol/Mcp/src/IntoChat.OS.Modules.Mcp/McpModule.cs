namespace IntoChat.OS.Modules.Mcp;

// McpServerNeuron is a plain neuron on the runtime's host grains. It opens its own client per call; the module needs no silo services.
[DigitalBrain.Module(ManifestName)]
public sealed class McpModule : DigitalBrain.Module
{
    internal const string ManifestName = "mcp";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [DigitalBrain.Need.HttpClient("mcp", requestsPerMinute: 120)],
        Settings: [],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts { get; } = typeof(IMcpServer).Assembly;
}
