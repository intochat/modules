using DigitalBrain.Capabilities;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Edge;

// Stateless edge validator for app invocation tokens: verifies HMAC-signed claims {brain, app, sha, exp}
// and stamps CapabilityCaller context without any grain actor hops.
internal sealed class EdgeSessionValidator(SessionTokens tokens, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public AppSessionClaims Validate(string brainId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedAccessException("The session token is missing, invalid or expired.");
        }

        var claims = tokens.ValidateAppToken(token)
            ?? throw new UnauthorizedAccessException("The session token is missing, invalid or expired.");

        if (!string.Equals(claims.Brain, brainId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The session token speaks for another brain.");
        }

        if (claims.Expires <= _time.GetUtcNow())
        {
            throw new UnauthorizedAccessException("The session token is missing, invalid or expired.");
        }

        return claims;
    }

    public CapabilityCaller Stamp(AppSessionClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var caller = CapabilityCallers.Of(
            principalId: AppPrincipal.Id(claims.App),
            accountId: claims.AccountId ?? claims.Brain,
            brainId: claims.Brain,
            kind: CallerKind.App,
            appId: claims.App);

        CallerContextStamper.Stamp(caller);
        return caller;
    }
}
