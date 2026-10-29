using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Runtime.Versioning;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Machine;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Framework.Parts;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Integrations;
using IntoChat.OS.Modules.Microsoft.Windows.Signals;
using Microsoft.AspNetCore.Routing;
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
        Settings: [DigitalBrain.Setting.Of<WindowsOptions>(IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static ConnectorDefinition Connector { get; } = new("windows", "Windows", ["device"], [])
    {
        Neurons = [typeof(IWindows), typeof(ILegacyStorage), typeof(IFileSystem), typeof(IConsole)],
        Signals = [typeof(FileWritten)],
    };

    public static Assembly? Contracts => typeof(IWindows).Assembly;

    public void Configure(IEndpointRouteBuilder endpoints) => WindowsEndpoints.Map(endpoints);

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<MachineIdentity>();
        services.TryAddSingleton<WindowsStorageCoordinator>();
        services.TryAddSingleton<IMachineIdentity>(provider => provider.GetRequiredService<MachineIdentity>());
        services.TryAddSingleton(MachineKeyStore);
        if (OperatingSystem.IsWindows()
            && Enum.TryParse<MasterKeyCustodyKind>(HostConfiguration.HostKey(configuration, IntoChatOSNames.CustodyConfigurationKey), ignoreCase: true, out var custody)
            && custody == MasterKeyCustodyKind.Tpm)
        { UseTpmCustody(services); }
    }

    // TPM custody when the provider can make keys; otherwise the master-key-sealed file next to the machine-id file,
    // so Custody reports "file" rather than failing.
    private static IMachineKeyStore MachineKeyStore(IServiceProvider provider)
    {
        if (OperatingSystem.IsWindows() && TpmMasterKeyCustody.IsAvailable()) { return new TpmMachineKeyStore(); }
        var file = provider.GetRequiredService<MachineIdentity>().File;
        return new FileMachineKeyStore(Path.GetDirectoryName(Path.GetFullPath(file))!, provider.GetRequiredService<IMasterKeyCustody>());
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
