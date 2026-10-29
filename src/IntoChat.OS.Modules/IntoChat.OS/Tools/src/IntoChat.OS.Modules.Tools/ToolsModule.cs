using Microsoft.Extensions.Configuration;
using IntoChat.OS.Connections.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Tools;

// The per-brain registry of tool sources, and the capability source that lets search and Jev see them.
[DigitalBrain.Module("tools")]
public sealed class ToolsModule : DigitalBrain.Module
{
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts { get; } = typeof(IToolSource).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRuntimeCapabilitySource, ToolCapabilitySource>());
    }
}
