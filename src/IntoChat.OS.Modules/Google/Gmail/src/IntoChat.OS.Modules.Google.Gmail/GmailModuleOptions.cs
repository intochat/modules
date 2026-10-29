using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Validation;

namespace IntoChat.OS.Modules.Google.Gmail;

public sealed record GmailModuleOptions : IModuleOptions
{
    public Uri GmailEndpoint { get; set; } = new("https://gmail.googleapis.com/gmail/v1/users/me/");
    public Uri ProfileEndpoint { get; set; } = new("https://gmail.googleapis.com/gmail/v1/users/me/profile");
    public bool HostGmail { get; set; }

    public GmailModuleOptions WithGmail() { HostGmail = true; return this; }

    public void Validate() => ModuleValidation.ThrowIfInvalid(this, new GmailOptionsValidator());
}

internal sealed class GmailOptionsValidator : IModuleValidator<GmailModuleOptions>
{
    public ValidationIssue[] Validate(GmailModuleOptions value)
    {
        var issues = new List<ValidationIssue>();
        Endpoint(nameof(value.GmailEndpoint), value.GmailEndpoint, true);
        Endpoint(nameof(value.ProfileEndpoint), value.ProfileEndpoint, false);
        return [.. issues];

        void Endpoint(string name, Uri? endpoint, bool directory)
        {
            if (endpoint is null || !endpoint.IsAbsoluteUri ||
                (endpoint.Scheme != "https" && (endpoint.Scheme != "http" || !endpoint.IsLoopback)) ||
                endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
                directory && !endpoint.AbsolutePath.EndsWith('/'))
            {
                issues.Add(new(name, "uri", directory
                    ? "Use an HTTPS base endpoint ending in '/'; HTTP is allowed only for loopback tests."
                    : "Use an HTTPS endpoint; HTTP is allowed only for loopback tests."));
            }
        }
    }
}
