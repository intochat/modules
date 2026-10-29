using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Framework.Modules.Metering;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using IntoChat.OS.Modules.Google.Auth;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Google.Gmail;

[DigitalBrain.Module("gmail")]
public sealed class GmailModule : DigitalBrain.Module, IHttpModule
{
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs:
        [
            DigitalBrain.Need.HttpClient("gmail", IntoChatOSConfiguration.ModuleOptionsSection("gmail") + ":ApiBaseUri", requestsPerMinute: 120),
            DigitalBrain.Need.Webhook(WatchPath, "gmail"),
        ],
        Settings: [DigitalBrain.Setting.Of<GmailModuleOptions>(IntoChatOSConfiguration.ModuleOptionsSection("gmail"))],
        Secrets: [])
    {
        Auth = new DigitalBrain.ModuleAuth("google-oauth", [GmailScopes.Read]),
    };

    public static System.Reflection.Assembly? Contracts => typeof(IGmail).Assembly;

    public static IntegrationSchema Integration { get; } = IntegrationSchema.For("google-pubsub", "Google Pub/Sub push")
        .RequiresSetting("Audience")
        .RequiresSetting("ServiceAccountEmail")
        .RequiresSetting("TopicName");

    public const string ClockKey = "google-gmail";
    public const string WatchPath = "/google/gmail/watch";

    public static ConnectorDefinition[] Connectors => [Gmail];

    public static ConnectorDefinition Gmail { get; } = new("gmail", "Gmail", ["google-oauth", "google-oauth-compose"], ["google-oauth"])
    {
        OptionalIntegrations = ["google-pubsub"],
        Neurons = [typeof(IGmail)],
        Signals = [typeof(MailReceived)],
        OAuthMethods = [GoogleAuth.OAuth("google-oauth", [GmailScopes.Read]),
            GoogleAuth.OAuth("google-oauth-compose", [GmailScopes.Read, "https://www.googleapis.com/auth/gmail.compose"])],
    };

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ModulePriceLists.AddModulePriceList<GooglePriceList>(services, "gmail", [
            new ExternalOperationDescriptor(GoogleOperations.Profile, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.ListMessages, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.Metadata, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.Message, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.CreateDraft, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.History, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.Watch, "google-oauth", "request"),
            new ExternalOperationDescriptor(GoogleOperations.Stop, "google-oauth", "request"),
        ]);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddKeyedSingleton(ClockKey, TimeProvider.System);
        services.TryAddSingleton(configuration.GetModuleOptions<GmailModuleOptions>("gmail"));
        services.TryAddSingleton<IGmailPushAuthenticator, GmailPushAuthenticator>();
        services.TryAddSingleton<IGmailApi>(provider => new GmailApi(provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<GmailModuleOptions>(), provider.GetRequiredKeyedService<TimeProvider>(ClockKey),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GmailApi>>()));
        GoogleAuth.Require(services, "gmail");
    }

    public void Configure(IEndpointRouteBuilder endpoints) => GmailWebhook.Map(endpoints);
}
