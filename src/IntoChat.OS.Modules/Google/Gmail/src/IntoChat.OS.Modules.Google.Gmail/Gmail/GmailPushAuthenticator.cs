using System.IdentityModel.Tokens.Jwt;
using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IntoChat.OS.Modules.Google.Gmail;

internal interface IGmailPushAuthenticator { Task<bool> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken); }
internal sealed class GmailPushAuthenticator(IModuleIntegrations<GmailModule> integrations) : IGmailPushAuthenticator
{
    // Issuer metadata is authentication control-plane work: there is no trusted selected brain before verification.
    // Share the issuer cache and bound its dedicated HTTPS transport instead of borrowing a route's payer authority.
    private static readonly HttpClient MetadataClient = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 64 * 1024 };
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> Configuration = new(
        "https://accounts.google.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever(MetadataClient) { RequireHttps = true });
    public async Task<bool> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (authorization.Length > 16 * 1024 + 7 || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) { return false; }
        var snapshot = await integrations.Get("google-pubsub").ReadAsync(cancellationToken);
        if (snapshot.Status != IntegrationStatus.Ready || !snapshot.Settings.TryGetValue("Audience", out var audience)
            || !snapshot.Settings.TryGetValue("ServiceAccountEmail", out var email)) { return false; }
        try
        {
            var metadata = await Configuration.GetConfigurationAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return ValidatePushToken(authorization[7..], metadata, audience, email);
        }
        catch (Exception error) when (error is SecurityTokenException or ArgumentException or InvalidOperationException or HttpRequestException) { return false; }
        catch (TimeoutException) { return false; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }
    internal static bool ValidatePushToken(string token, OpenIdConnectConfiguration metadata, string audience, string email)
    {
        if (token.Length > 16 * 1024) { return false; }
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = metadata.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(1),
        }, out _);
        return principal.FindFirst("email")?.Value == email && principal.FindFirst("email_verified")?.Value is "true" or "True";
    }
}
