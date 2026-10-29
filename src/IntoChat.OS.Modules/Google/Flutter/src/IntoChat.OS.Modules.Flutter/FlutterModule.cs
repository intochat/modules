using IntoChat.OS;
using IntoChat.OS.Modules.Flutter.Space;
using IntoChat.OS.Modules.Flutter.Workspace;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Hosting;

namespace IntoChat.OS.Modules.Flutter;

[DigitalBrain.Module(ModuleName)]
public sealed class FlutterModule : DigitalBrain.Module, IHttpModule
{
    internal const string ModuleName = "flutter";

    public static System.Reflection.Assembly? Contracts { get; } = typeof(IntoChat.OS.Modules.Flutter.Button.IButton).Assembly;

    // R4-04: what the module needs from its host. Registrations of neurons that are still grains (Chat, Composer, Space) stay here.
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<FlutterModuleOptions>(IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId(ModuleName)))],
        Secrets: []);

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton<CircleMembers>();
        services.AddHostedService<SpaceWatcher>();
    }

    public void Configure(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        WorkspaceEndpoints.Map(endpoints);
        LiveSignalEndpoints.Map(endpoints);
        BrainEventsEndpoints.Map(endpoints);
        AppUiEndpoints.Map(endpoints);
        Settings.NotificationPreferenceEndpoints.Map(endpoints);
        Settings.WorkspacePreferenceEndpoints.Map(endpoints);
        Settings.ChatEndpoints.Map(endpoints);
    }
}
