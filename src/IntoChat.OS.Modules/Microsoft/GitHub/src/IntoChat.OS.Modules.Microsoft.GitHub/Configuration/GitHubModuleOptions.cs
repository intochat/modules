using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

// Public deployment metadata only. The App id, key, OAuth client and webhook secret are the
// github Integration's; repository connections are per-brain neurons.
public sealed class GitHubModuleOptions : IModuleOptions
{
    public static readonly string SectionName = IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId("github"));

    // Host-side App registration (read by WithConfiguredGitHubApp); zero AppId means not configured.
    [Setting] public long AppId { get; set; }
    [Setting] public string ClientId { get; set; } = "";
    [Setting] public Uri? PublicOrigin { get; set; }
    [Setting] public string PublicWebhookUrl { get; set; } = "";

    public void Validate()
    {
        if (PublicWebhookUrl.Length != 0 && (!Uri.TryCreate(PublicWebhookUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"))
        { throw new ArgumentException("The GitHub webhook URL must be an absolute HTTPS URL."); }
    }
}
