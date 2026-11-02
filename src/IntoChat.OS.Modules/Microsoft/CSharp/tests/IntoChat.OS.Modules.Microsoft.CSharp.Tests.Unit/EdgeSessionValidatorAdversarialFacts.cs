using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitalBrain.Capabilities;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Edge;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class EdgeSessionValidatorAdversarialFacts : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "os-adversarial-validator-" + Guid.NewGuid().ToString("N"));
    private readonly AdversarialTimeProvider _time = new();
    private readonly byte[] _configuredKey = RandomNumberGenerator.GetBytes(32);

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    #region 1. Cryptographic Tampering Rejection

    [Fact]
    public async Task TamperedSignature_SingleBitFlip_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-12345");
        var parts = token.Split('.');
        Assert.Equal(2, parts.Length);

        // Decode signature bytes, flip the least significant bit of first byte, re-encode
        var sigBytes = FromBase64Url(parts[1]);
        sigBytes[0] ^= 0x01;
        var tamperedToken = parts[0] + "." + Base64Url(sigBytes);

        Assert.Null(tokens.ValidateAppToken(tamperedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedSignature_Truncated_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-12345");
        var parts = token.Split('.');

        // Truncate signature
        var truncatedToken = parts[0] + "." + parts[1][..(parts[1].Length / 2)];

        Assert.Null(tokens.ValidateAppToken(truncatedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", truncatedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedSignature_AppendedBytes_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-12345");
        var parts = token.Split('.');

        var appendedToken = parts[0] + "." + parts[1] + "extra";

        Assert.Null(tokens.ValidateAppToken(appendedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", appendedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedSignature_ReplacedWithAllZeros_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-12345");
        var parts = token.Split('.');

        var zeroSigToken = parts[0] + "." + Base64Url(new byte[32]);

        Assert.Null(tokens.ValidateAppToken(zeroSigToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", zeroSigToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedSignature_DifferentKey_Rejected()
    {
        var tokens1 = CreateTokens();
        await tokens1.InitializeAsync(CancellationToken.None);

        var differentKey = RandomNumberGenerator.GetBytes(32);
        var tokens2 = CreateTokensWithKey(differentKey);
        await tokens2.InitializeAsync(CancellationToken.None);
        var validator2 = new EdgeSessionValidator(tokens2, _time);

        // Issued with key 1, validated with key 2
        var token = tokens1.IssueAppToken("brain-alpha", "weather-app", "sha-12345");

        Assert.Null(tokens2.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator2.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedClaims_BrainModified_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-original", "weather-app", "sha-12345", "account-1");
        var parts = token.Split('.');

        var claimsJson = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
        var tamperedJson = claimsJson.Replace("brain-original", "brain-attacker");
        var tamperedToken = Base64Url(Encoding.UTF8.GetBytes(tamperedJson)) + "." + parts[1];

        Assert.Null(tokens.ValidateAppToken(tamperedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-attacker", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedClaims_AppModified_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-original", "weather-app", "sha-12345", "account-1");
        var parts = token.Split('.');

        var claimsJson = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
        var tamperedJson = claimsJson.Replace("weather-app", "admin-app");
        var tamperedToken = Base64Url(Encoding.UTF8.GetBytes(tamperedJson)) + "." + parts[1];

        Assert.Null(tokens.ValidateAppToken(tamperedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-original", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedClaims_ShaModified_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-original", "weather-app", "sha-original-123", "account-1");
        var parts = token.Split('.');

        var claimsJson = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
        var tamperedJson = claimsJson.Replace("sha-original-123", "sha-tampered-999");
        var tamperedToken = Base64Url(Encoding.UTF8.GetBytes(tamperedJson)) + "." + parts[1];

        Assert.Null(tokens.ValidateAppToken(tamperedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-original", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedClaims_ExpiresModified_ExtendedLifetime_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-original", "weather-app", "sha-123", "account-1");
        var parts = token.Split('.');

        var claims = JsonSerializer.Deserialize<AppSessionClaims>(FromBase64Url(parts[0]), JsonSerializerOptions.Web)!;
        var extendedClaims = claims with { Expires = _time.GetUtcNow() + TimeSpan.FromDays(3650) };
        var extendedPayload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(extendedClaims, JsonSerializerOptions.Web));
        var tamperedToken = extendedPayload + "." + parts[1];

        Assert.Null(tokens.ValidateAppToken(tamperedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-original", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedClaims_AccountIdModified_PrivilegeEscalation_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-original", "weather-app", "sha-123", "low-privilege-user");
        var parts = token.Split('.');

        var claimsJson = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
        var tamperedJson = claimsJson.Replace("low-privilege-user", "root-super-admin");
        var tamperedToken = Base64Url(Encoding.UTF8.GetBytes(tamperedJson)) + "." + parts[1];

        Assert.Null(tokens.ValidateAppToken(tamperedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-original", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossToken_SignatureReplay_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var tokenA = tokens.IssueAppToken("brain-A", "app-A", "sha-A");
        var tokenB = tokens.IssueAppToken("brain-B", "app-B", "sha-B");

        var partsA = tokenA.Split('.');
        var partsB = tokenB.Split('.');

        // Signature of A on payload of B
        var forgedToken = partsB[0] + "." + partsA[1];

        Assert.Null(tokens.ValidateAppToken(forgedToken));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-B", forgedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 2. Expiration & Clock Skew Boundaries

    [Fact]
    public async Task Expiration_ExactExpirationInstant_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-123");

        // Advance time to exactly the expiration timestamp
        _time.Now += SessionTokens.Lifetime;

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expiration_OneTickPastExpiration_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-123");

        // Advance time to 1 tick past expiration
        _time.Now += SessionTokens.Lifetime + TimeSpan.FromTicks(1);

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expiration_OneTickBeforeExpiration_Accepted()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-123");

        // Advance time to 1 tick before expiration
        _time.Now += SessionTokens.Lifetime - TimeSpan.FromTicks(1);

        var claims = tokens.ValidateAppToken(token);
        Assert.NotNull(claims);
        var validated = validator.Validate("brain-alpha", token);
        Assert.Equal("brain-alpha", validated.Brain);
    }

    [Fact]
    public async Task Expiration_UnixEpoch_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        // Craft a token with Expires = UnixEpoch signed with the real key
        var expiredClaims = new AppSessionClaims("brain-alpha", "weather-app", "sha-123", DateTimeOffset.UnixEpoch);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(expiredClaims, JsonSerializerOptions.Web);
        var sigBytes = HMACSHA256.HashData(_configuredKey, payloadBytes);
        var token = Base64Url(payloadBytes) + "." + Base64Url(sigBytes);

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expiration_MinValue_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var expiredClaims = new AppSessionClaims("brain-alpha", "weather-app", "sha-123", DateTimeOffset.MinValue);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(expiredClaims, JsonSerializerOptions.Web);
        var sigBytes = HMACSHA256.HashData(_configuredKey, payloadBytes);
        var token = Base64Url(payloadBytes) + "." + Base64Url(sigBytes);

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expiration_ValidatorClockAheadOfTokensClock_Rejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);

        var token = tokens.IssueAppToken("brain-alpha", "weather-app", "sha-123");

        // Validator has a skewed clock ahead by 2 hours
        var fastClock = new AdversarialTimeProvider { Now = _time.Now + TimeSpan.FromHours(2) };
        var validator = new EdgeSessionValidator(tokens, fastClock);

        // tokens still considers it valid under _time, but validator rejects under fastClock
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 3. Cross-Brain & Cross-App Spoofing

    [Fact]
    public async Task CrossBrain_ValidTokenForBrainA_RejectedAgainstBrainB()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain-tenant-A", "weather-app", "sha-123");

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-tenant-B", token));
        Assert.Contains("speaks for another brain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossBrain_OrdinalCaseSensitivityEnforced()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("tenant-alpha", "weather-app", "sha-123");

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("TENANT-ALPHA", token));
        Assert.Contains("speaks for another brain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespaceBrainClaim_Rejected(string badBrain)
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var claims = new AppSessionClaims(badBrain, "weather-app", "sha-123", _time.GetUtcNow() + TimeSpan.FromHours(1));
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(claims, JsonSerializerOptions.Web);
        var sigBytes = HMACSHA256.HashData(_configuredKey, payloadBytes);
        var token = Base64Url(payloadBytes) + "." + Base64Url(sigBytes);

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(badBrain, token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespaceAppClaim_Rejected(string badApp)
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var claims = new AppSessionClaims("brain-alpha", badApp, "sha-123", _time.GetUtcNow() + TimeSpan.FromHours(1));
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(claims, JsonSerializerOptions.Web);
        var sigBytes = HMACSHA256.HashData(_configuredKey, payloadBytes);
        var token = Base64Url(payloadBytes) + "." + Base64Url(sigBytes);

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 4. Malformed / Corrupted Payloads & Edge Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("nodotintoken")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData("part1.")]
    [InlineData(".part2")]
    [InlineData("part1.part2.part3")]
    [InlineData("part1.part2.part3.part4")]
    [InlineData("invalid base64!@#.invalid base64!@#")]
    [InlineData("===.===")]
    public async Task MalformedToken_RejectedSafelyWithoutCrash(string? malformed)
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        Assert.Null(tokens.ValidateAppToken(malformed));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", malformed));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidBase64_NonJsonPayload_RejectedSafely()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var nonJsonPayload = Base64Url(Encoding.UTF8.GetBytes("Not A JSON String At All"));
        var randomSig = Base64Url(RandomNumberGenerator.GetBytes(32));
        var token = nonJsonPayload + "." + randomSig;

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidBase64_SignedNonJsonPayload_RejectedSafely()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var nonJsonBytes = Encoding.UTF8.GetBytes("This is plaintext, definitely not json");
        var sigBytes = HMACSHA256.HashData(_configuredKey, nonJsonBytes);
        var token = Base64Url(nonJsonBytes) + "." + Base64Url(sigBytes);

        // Even though signed with Key, deserialization of non-JSON or missing claims must fail safely
        try
        {
            var result = tokens.ValidateAppToken(token);
            Assert.Null(result);
        }
        catch (JsonException)
        {
            // Expected if deserialization strictly throws on invalid JSON payload
        }
    }

    [Fact]
    public async Task LargePayload_1MB_RejectedSafely()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var largePayload = Base64Url(new byte[1024 * 1024]);
        var randomSig = Base64Url(new byte[32]);
        var token = largePayload + "." + randomSig;

        Assert.Null(tokens.ValidateAppToken(token));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain-alpha", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NullByteInjection_InClaims_RejectedSafely()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain\0alpha", "app\0beta", "sha\0hash");
        // Token has null bytes in strings; if validated against "brain\0alpha", claims match
        var claims = validator.Validate("brain\0alpha", token);
        Assert.Equal("brain\0alpha", claims.Brain);

        // But when validated against "brain", must reject
        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain", token));
        Assert.Contains("speaks for another brain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 5. Legacy Token vs App Token Interoperability

    [Fact]
    public async Task LegacySessionToken_ValidateAppToken_ReturnsNullWithoutThrowing()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);

        // Issue a legacy token (SessionClaims {Brain, Session, Expires})
        var legacyToken = tokens.Issue("brain-alpha", "session-xyz");

        // Validate as legacy token -> succeeds
        var legacyClaims = tokens.Validate(legacyToken);
        Assert.NotNull(legacyClaims);
        Assert.Equal("brain-alpha", legacyClaims.Brain);
        Assert.Equal("session-xyz", legacyClaims.Session);

        // Validate as AppToken -> must return null (because App is null/empty) and NOT throw
        var appClaims = tokens.ValidateAppToken(legacyToken);
        Assert.Null(appClaims);
    }

    [Fact]
    public async Task AppSessionToken_ValidateLegacy_ReturnsNullWithoutThrowing()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);

        // Issue an app token (AppSessionClaims {Brain, App, Sha, Expires, AccountId})
        var appToken = tokens.IssueAppToken("brain-alpha", "app-1", "sha-1");

        // Validate as app token -> succeeds
        var appClaims = tokens.ValidateAppToken(appToken);
        Assert.NotNull(appClaims);

        // Validate as legacy token -> SessionClaims expects "session", which is null in AppSessionClaims!
        // In SessionTokens.Validate:
        // var parsed = JsonSerializer.Deserialize<SessionClaims>(claims, JsonSerializerOptions.Web);
        // return parsed is not null && parsed.Expires > time.GetUtcNow() ? parsed : null;
        // Even if deserialized, Session is null.
        var legacy = tokens.Validate(appToken);
        // Either null or parsed with Session = null
        Assert.True(legacy is null || legacy.Session is null);
    }

    #endregion

    #region 6. Caller Stamping Invariants

    [Fact]
    public async Task CallerStamping_EnforcesAllSecurityFields()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var claims = new AppSessionClaims("brain-corp", "accounting-app", "sha-hash-secure", _time.GetUtcNow() + TimeSpan.FromHours(1), "customer-acc-99");
        var caller = validator.Stamp(claims);

        Assert.Equal(CallerKind.App, caller.Kind);
        Assert.Equal(AppPrincipal.Id("accounting-app"), caller.PrincipalId);
        Assert.Equal("accounting-app", caller.AppId);
        Assert.Equal("brain-corp", caller.BrainId);
        Assert.Equal("customer-acc-99", caller.AccountId);

        // Assert async context stamping
        Assert.True(CallerContextStamper.TryGet(out var activeContext));
        Assert.Equal(caller, activeContext);
    }

    [Fact]
    public async Task CallerStamping_NullClaims_ThrowsArgumentNullException()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        Assert.Throws<ArgumentNullException>(() => validator.Stamp(null!));
    }

    #endregion

    #region 7. RuntimeEdge Integration Invariants

    [Fact]
    public async Task RuntimeEdge_CrossAppSpoofing_ThrowsUnauthorizedAccessException()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);
        var edge = new RuntimeEdge(tokens, null!, null!, null!, null!, null!, validator);

        var token = tokens.IssueAppToken("brain-1", "legit-app", "sha-1");
        var invocation = new IntoChat.OS.Client.Runtime.V1.RuntimeInvocation(
            "malicious-impersonator-app",
            "SomeContract",
            "SomeMethod",
            "key",
            []);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            edge.InvokeAsync("brain-1", token, invocation, CancellationToken.None));
        Assert.Contains("cannot invoke as 'malicious-impersonator-app'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeEdge_CrossBrainSpoofing_ThrowsUnauthorizedAccessException()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);
        var edge = new RuntimeEdge(tokens, null!, null!, null!, null!, null!, validator);

        var token = tokens.IssueAppToken("brain-A", "app-1", "sha-1");
        var invocation = new IntoChat.OS.Client.Runtime.V1.RuntimeInvocation(
            "app-1",
            "SomeContract",
            "SomeMethod",
            "key",
            []);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            edge.InvokeAsync("brain-B", token, invocation, CancellationToken.None));
        Assert.Contains("speaks for another brain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeEdge_TamperedToken_ThrowsUnauthorizedAccessException()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);
        var edge = new RuntimeEdge(tokens, null!, null!, null!, null!, null!, validator);

        var token = tokens.IssueAppToken("brain-1", "app-1", "sha-1");
        var tamperedToken = token + "corrupted";
        var invocation = new IntoChat.OS.Client.Runtime.V1.RuntimeInvocation(
            "app-1",
            "SomeContract",
            "SomeMethod",
            "key",
            []);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            edge.InvokeAsync("brain-1", tamperedToken, invocation, CancellationToken.None));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeEdge_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);
        var edge = new RuntimeEdge(tokens, null!, null!, null!, null!, null!, validator);

        var token = tokens.IssueAppToken("brain-1", "app-1", "sha-1");
        _time.Now += SessionTokens.Lifetime + TimeSpan.FromMinutes(1);

        var invocation = new IntoChat.OS.Client.Runtime.V1.RuntimeInvocation(
            "app-1",
            "SomeContract",
            "SomeMethod",
            "key",
            []);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            edge.InvokeAsync("brain-1", token, invocation, CancellationToken.None));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    private SessionTokens CreateTokens() => CreateTokensWithKey(_configuredKey);

    private SessionTokens CreateTokensWithKey(byte[] key)
    {
        var secret = Convert.ToBase64String(key);
        var values = new Dictionary<string, string?>
        {
            [IntoChatOSNames.HomeKey] = _home,
            [IntoChatOSNames.TopologyKey] = IntoChatOSNames.TopologyCloud,
        };

        return new SessionTokens(
            new ReadyIntegrations(secret),
            _time,
            new Env(Environments.Production),
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new Quiet());
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }

    private sealed class AdversarialTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Quiet : ILogger<SessionTokens>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class ReadyIntegrations(string secret) : IModuleIntegrations<CSharpModule>
    {
        public IIntegrationHandle Get(string integrationId) => new Ready(secret);

        private sealed class Ready(string secret) : IIntegrationHandle
        {
            public string IntegrationId => "csharp";
            public Task<IntegrationSnapshot> ReadAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(new IntegrationSnapshot { IntegrationId = "csharp", Status = IntegrationStatus.Ready });
            public Task<string> ReadSecretAsync(string field, CancellationToken cancellationToken = default) => Task.FromResult(secret);
            public Task<ReleasedIntegration> ReleaseAsync(CancellationToken cancellationToken = default)
                => throw new InvalidOperationException();
        }
    }
}
