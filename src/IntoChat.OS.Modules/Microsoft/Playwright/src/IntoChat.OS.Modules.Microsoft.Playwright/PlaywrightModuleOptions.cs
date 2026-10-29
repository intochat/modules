using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Microsoft.Playwright;

// Origins of apps the deployment itself serves (e.g. ArtCraft web builds on loopback). Pages on these
// origins load directly instead of through the public-only proxy, and only they accept Evaluate.
public sealed class PlaywrightModuleOptions : IModuleOptions
{
    public string[] AppOrigins { get; set; } = [];

    public string? ChromiumExecutablePath { get; set; }
    public int MaxManagedAccounts { get; set; } = 8;
    public int MaxManagedSessionsPerAccount { get; set; } = 4;
    public TimeSpan ManagedSessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public void Validate()
    {
        if (MaxManagedAccounts is < 1 or > 64 || MaxManagedSessionsPerAccount is < 1 or > 16
            || ManagedSessionIdleTimeout < TimeSpan.FromSeconds(1) || ManagedSessionIdleTimeout > TimeSpan.FromHours(1))
        { throw new ArgumentException("Managed browser limits are outside the supported range."); }
        foreach (var origin in AppOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
            { throw new ArgumentException($"App origin '{origin}' must be a bare http(s) origin."); }
        }
    }

    internal bool IsAppPage(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && AppOrigins.Any(origin => Uri.TryCreate(origin, UriKind.Absolute, out var allowed)
                && Uri.Compare(uri, allowed, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0);
}
