using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.Playwright;

[DigitalBrain.Module(ManifestName)]
public sealed class PlaywrightModule : DigitalBrain.Module
{
    internal const string ManifestName = "playwright";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<PlaywrightModuleOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts => typeof(IPlaywright).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPlaywright();
    }
}

public static class PlaywrightHosting
{
    public static IServiceCollection AddPlaywright(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IBrowserSessionProvider, PlaywrightSessionProvider>();
        return services;
    }
}
