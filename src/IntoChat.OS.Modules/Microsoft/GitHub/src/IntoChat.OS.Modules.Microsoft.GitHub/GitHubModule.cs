using Microsoft.Extensions.Configuration;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using IntoChat.OS.Modules.Microsoft.GitHub.Webhook;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

[DigitalBrain.Module(ManifestName)]
public sealed class GitHubModule : DigitalBrain.Module
{
    internal const string ManifestName = "github";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs:
        [
            DigitalBrain.Need.HttpClient("github", requestsPerMinute: 120),
            DigitalBrain.Need.Webhook("/github/webhook", "github.repository"),
        ],
        Settings: [DigitalBrain.Setting.Of<GitHubModuleOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: [])
    {
        Auth = new DigitalBrain.ModuleAuth("github", []),
    };

    public static System.Reflection.Assembly? Contracts => typeof(IRepository).Assembly;

    // ClientId sits beside ClientSecret so the Platform's OAuth flow reads both from one release.
    public static IntegrationSchema Integration { get; } = IntegrationSchema.For("github", "GitHub")
        .RequiresSecret("PrivateKeyPem")
        .RequiresSetting("AppId")
        .OffersSetting("ClientId")
        .OffersSecret("ClientSecret")
        .OffersSecret("WebhookSecret");

    // GitHub App permissions belong to the installation, so the user authorization asks for no scopes.
    public static ConnectorDefinition Connector => new("github", "GitHub", ["oauth"], ["github"])
    {
        Neurons = [typeof(IRepository)],
        Signals = [typeof(Signals.PullRequestChanged)],
        OAuthMethods = [new("oauth", "https://github.com/login/oauth/authorize", "https://github.com/login/oauth/access_token", [], "github", "ClientId", "ClientSecret")],
    };

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton<GitHubAppRegistration>();
        services.AddSingleton<GitHubInstallationTokens>();
        services.AddSingleton<IGitHubInstallationVerifier>(services => services.GetRequiredService<GitHubInstallationTokens>());
        services.AddSingleton<IGitHubRepositorySource, GitHubRepositorySource>();
        services.AddSingleton<IGitHubUserAccess, GitHubUserAccess>();
        services.AddSingleton<GitHubWebhookIngress>();
        services.AddSingleton<IHttpSurface, GitHubWebhookSurface>();
        services.AddHttpClient(GitHubUserAccess.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    }
}
