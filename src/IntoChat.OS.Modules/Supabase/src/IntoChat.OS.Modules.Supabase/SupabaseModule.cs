using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Supabase;

// Supabase is a deployment backing store. A person's Supabase database is a postgres connection.
[DigitalBrain.Module(SupabaseModule.ManifestName)]
public sealed class SupabaseModule : DigitalBrain.Module
{
    internal const string ManifestName = "supabase";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<SupabaseModuleOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(SupabaseModule.ManifestName))],
        Secrets: []);

    public static bool RequiresContainer => true;

    public static IntegrationSchema Integration => IntegrationSchema.For("supabase", "Supabase").RequiresSecret("ConnectionString");

    public const string ConnectionName = "supabase";
    public const string ProviderName = "Npgsql";

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<SupabaseModuleOptions>()
            .Validate(static options => string.Equals(options.Provider, ProviderName, StringComparison.OrdinalIgnoreCase),
                "Supabase requires the Npgsql provider.");
        services.TryAddSingleton<SupabaseBackingStore>();
        services.TryAddSingleton<ISupabaseBackingStore>(provider => provider.GetRequiredService<SupabaseBackingStore>());
        services.AddSingleton<IIntegrationStartup>(provider => provider.GetRequiredService<SupabaseBackingStore>());
        services.AddHealthChecks().AddCheck<SupabaseHealthCheck>("supabase", tags: ["ready"]);
    }
}
