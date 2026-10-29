using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.ClickHouse;

[DigitalBrain.Module(ClickHouseModule.ManifestName)]
public sealed class ClickHouseModule : DigitalBrain.Module
{
    internal const string ManifestName = "clickhouse";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<ClickHouseModuleOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(ClickHouseModule.ManifestName))],
        Secrets: []);

    public static bool RequiresContainer => true;

    public static System.Reflection.Assembly? Contracts => typeof(Query.IClickHouse).Assembly;

    public static IntegrationSchema Integration => IntegrationSchema.For("clickhouse", "ClickHouse").RequiresSecret("ConnectionString");
    public static ConnectorDefinition Connector => new("clickhouse", "ClickHouse", ["connection-string"], ["clickhouse"]) { Neurons = [typeof(Query.IClickHouse)], Signals = [] };

    public const string DriverProviderName = "ClickHouse";

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<ClickHouseModuleOptions>()
            .Validate(static options => string.Equals(options.Provider, DriverProviderName, StringComparison.OrdinalIgnoreCase),
                "ClickHouse requires the module option Provider=ClickHouse.");
        services.AddHttpClient(ClickHouseRegistration.HttpClientName, static client => client.Timeout = TimeSpan.FromMinutes(2));
        services.TryAddSingleton<IntegrationClickHouseProvider>();
        services.TryAddSingleton<IClickHouseProvider>(provider => provider.GetRequiredService<IntegrationClickHouseProvider>());
        services.AddSingleton<IIntegrationStartup>(provider => provider.GetRequiredService<IntegrationClickHouseProvider>());
        services.TryAddSingleton<IClickHouseSourceFactory, ClickHouseClientSourceFactory>();
        services.AddHealthChecks().AddCheck<ClickHouseHealthCheck>("clickhouse", tags: ["ready"]);
    }
}
