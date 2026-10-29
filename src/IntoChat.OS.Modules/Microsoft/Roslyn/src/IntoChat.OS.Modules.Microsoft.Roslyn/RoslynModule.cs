using IntoChat.OS.Framework.Modules.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.Roslyn;

[DigitalBrain.Module(ManifestName)]
public sealed class RoslynModule : DigitalBrain.Module
{
    internal const string ManifestName = "roslyn";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<RoslynModuleOptions>(IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts => typeof(IRoslyn).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<RoslynModuleOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.WorkspaceKey), "Roslyn workspace key must not be empty.")
            .ValidateOnStart();
        services.TryAddSingleton<SolutionWorkspace>();
        services.TryAddSingleton<ISolutionLoader, MSBuildSolutionLoader>();
        services.TryAddSingleton<CodeFixCatalog>();
        services.TryAddSingleton<ChangeSetEditor>();
        services.TryAddSingleton<SolutionFileWatcher>();
        services.AddHostedService<WorkspaceWarmup>();
    }
}
