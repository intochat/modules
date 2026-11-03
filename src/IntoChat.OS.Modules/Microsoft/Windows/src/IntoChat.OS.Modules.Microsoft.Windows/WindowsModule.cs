using System.Reflection;
using System.Runtime.Versioning;
using DigitalBrain.Capabilities.Connections.Connectors;
using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Identity.Local.Custody;
using DigitalBrain.Modules.Configuration;
using DigitalBrain.Runtime.Http.Surface;
using IntoChat.OS.Modules.Microsoft.Windows.Signals;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// A Windows PC as an edge: the machine, its files and its console, served only by a silo on that PC.
[DigitalBrain.Module(ManifestName)]
public sealed class WindowsModule : DigitalBrain.Module, IHttpModule
{
    internal const string ManifestName = "windows";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [DigitalBrain.Capabilities.Capability.Storage.V1],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<WindowsOptions>(ModuleConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static ConnectorDefinition Connector { get; } = new("windows", "Windows", ["device"], [])
    {
        Neurons = [typeof(IWindows), typeof(IStorage), typeof(IFileSystem), typeof(IConsole)],
        Signals = [typeof(FileWritten)],
    };

    public static Assembly? Contracts => typeof(IWindows).Assembly;

    public void Configure(IEndpointRouteBuilder endpoints) => WindowsEndpoints.Map(endpoints);

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<MachineIdentity>();
        services.TryAddSingleton<WindowsStorageCoordinator>();
        var runtimeOptions = DigitalBrain.Runtime.Orleans.RuntimeOptions.Resolve(services);
        if (OperatingSystem.IsWindows()
            && Enum.TryParse<MasterKeyCustodyKind>(HostConfiguration.HostKey(configuration, runtimeOptions.CustodyConfigurationKey), ignoreCase: true, out var custody)
            && custody == MasterKeyCustodyKind.Tpm)
        { UseTpmCustody(services); }
    }

    // The local brain key stays wrapped by the TPM. Missing hardware refuses startup.
    [SupportedOSPlatform("windows")]
    private static void UseTpmCustody(IServiceCollection services)
    {
        services.RemoveAll<IMasterKeyCustody>();
        services.AddSingleton<IMasterKeyCustody>(_ => TpmMasterKeyCustody.IsAvailable()
            ? new TpmMasterKeyCustody()
            : throw new InvalidOperationException("The Microsoft Platform Crypto Provider (TPM) is not available on this machine."));
    }
}
