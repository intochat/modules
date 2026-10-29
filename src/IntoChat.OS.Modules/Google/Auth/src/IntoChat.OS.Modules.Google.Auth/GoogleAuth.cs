using IntoChat.OS.Connections.Platform.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Google.Auth;

public static class GoogleAuth
{
    public const string Integration = "google-oauth";

    // One browser sign-in method against Google; offline access so the refresh token is issued.
    public static OAuthMethod OAuth(string id, string[] scopes) => new(id,
        "https://accounts.google.com/o/oauth2/v2/auth", "https://oauth2.googleapis.com/token", scopes, Integration)
    {
        AuthorizationParameters = { ["access_type"] = "offline", ["prompt"] = "consent" },
        ExternalOperations = new(GoogleAuthOperations.CodeExchange, GoogleAuthOperations.Refresh),
    };

    // A Google module needs google-auth in the same silo; the check runs at host start, so module order does not matter.
    public static void Require(IServiceCollection services, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        services.AddOptions<GoogleAuthRequirement>(moduleId)
            .Validate<IServiceProvider>((_, provider) => provider.GetService<GoogleAuthRegistration>() is not null,
                $"{moduleId} requires the {GoogleAuthModule.ModuleName} module")
            .ValidateOnStart();
    }

    // Throws now when google-auth is missing; startup validation calls the same check.
    public static void EnsureRegistered(IServiceProvider services, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(services);
        try { _ = services.GetRequiredService<IOptionsMonitor<GoogleAuthRequirement>>().Get(moduleId); }
        catch (OptionsValidationException error) { throw new InvalidOperationException(error.Message, error); }
    }
}

public static class GoogleAuthOperations
{
    public const string CodeExchange = "google.oauth.code.exchange";
    public const string Refresh = "google.oauth.token.refresh";
}

public static class GoogleHttp
{
    // The metered client Gmail and Drive call Google through; registered once by google-auth.
    public const string ClientName = "IntoChat.OS.Google";
}

// Present only when google-auth is composed into the silo.
public sealed class GoogleAuthRegistration;

public sealed class GoogleAuthRequirement;
