using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

// The Pub/Sub push token check, with a symmetric key standing in for Google's published keys: no HTTP.
public sealed class GmailPushAuthenticatorFacts
{
    private const string Audience = "https://example.test/push";
    private const string Email = "push@project.iam.gserviceaccount.com";
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("unit-test-signing-key-0123456789-abcdef"));
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void AValidSignedTokenForTheConfiguredAudienceAndServiceAccountIsAccepted()
        => Assert.True(Validate(Token()));

    [Fact]
    public void AnUnverifiedEmailIsRejected()
        => Assert.False(Validate(Token(verified: "false")));

    [Fact]
    public void ADifferentServiceAccountIsRejected()
        => Assert.False(Validate(Token(email: "other@project.iam.gserviceaccount.com")));

    [Fact]
    public void AWrongAudienceIssuerOrExpiredTokenThrows()
    {
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(Token(audience: "https://elsewhere.test")));
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(Token(issuer: "https://evil.test")));
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(Token(expires: Now.AddHours(-1), notBefore: Now.AddHours(-2))));
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyThrows()
    {
        var other = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-signing-key-0123456789-abcdefgh"));
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(Token(signingKey: other)));
    }

    [Fact]
    public void AnOversizedSignedTokenIsRejectedBeforeClaimProcessing()
    {
        var token = new JwtSecurityToken("https://accounts.google.com", Audience,
            [new Claim("email", Email), new Claim("email_verified", "true"), new Claim("padding", new string('x', 17000))],
            Now.AddMinutes(-1), Now.AddHours(1), new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        Assert.False(Validate(new JwtSecurityTokenHandler().WriteToken(token)));
    }

    private static bool Validate(string token)
    {
        var metadata = new OpenIdConnectConfiguration();
        metadata.SigningKeys.Add(Key);
        return GmailPushAuthenticator.ValidatePushToken(token, metadata, Audience, Email);
    }

    private static string Token(string email = Email, string verified = "true", string audience = Audience,
        string issuer = "https://accounts.google.com", DateTime? expires = null, DateTime? notBefore = null, SecurityKey? signingKey = null)
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer, audience,
            [new Claim("email", email), new Claim("email_verified", verified)],
            notBefore ?? Now.AddMinutes(-1), expires ?? Now.AddHours(1),
            new SigningCredentials(signingKey ?? Key, SecurityAlgorithms.HmacSha256)));
}
