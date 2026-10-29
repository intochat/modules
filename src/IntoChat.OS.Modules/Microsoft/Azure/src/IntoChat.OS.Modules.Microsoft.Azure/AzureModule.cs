using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.Azure;

// An Azure subscription as a connection: it ensures the resources the OS and its apps run on.
[DigitalBrain.Module(ManifestName)]
public sealed class AzureModule : DigitalBrain.Module
{
    internal const string ManifestName = "azure";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<AzureOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static ConnectorDefinition Connector { get; } = new("azure", "Azure", ["identity"], [])
    {
        Neurons = [typeof(IAzure)],
    };

    public static Assembly? Contracts => typeof(IAzure).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton<IAzureArm, ArmAzure>();
    }
}
