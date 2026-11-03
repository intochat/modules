using DigitalBrain.Capabilities.Compute;
using DigitalBrain.Capabilities.Connections.Connectors;
using DigitalBrain.Modules.Configuration;
using DigitalBrain.Modules.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Google.Auth;

// Google sign-in shared by Gmail and Drive: the one google-oauth integration, its client secrets,
// the token exchange and refresh operations and the metered Google HTTP client.
[DigitalBrain.Module(ModuleName)]
public sealed class GoogleAuthModule : DigitalBrain.Module
{
    public const string ModuleName = "google-auth";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<GoogleAuthModuleOptions>(ModuleConfiguration.ModuleOptionsSection(ModuleName))],
        Secrets: []);

    public static IntegrationSchema Integration { get; } = IntegrationSchema.For(GoogleAuth.Integration, "Google OAuth")
        .RequiresSecret("ClientId")
        .RequiresSecret("ClientSecret");

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ModulePriceLists.AddModulePriceList<GooglePriceList>(services, ModuleName, [
            new ExternalOperationDescriptor(GoogleAuthOperations.CodeExchange, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleAuthOperations.Refresh, GoogleAuth.Integration, "request"),
        ]);
        services.TryAddSingleton<GoogleAuthRegistration>();
        services.AddMeteredHttpClient(GoogleHttp.ClientName).ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
    }
}

public sealed record GoogleAuthModuleOptions : IModuleOptions
{
    public void Validate() { }
}
