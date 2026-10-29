using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Platform.Integrations;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

internal sealed record GitHubAppCredentials(long AppId, string PrivateKeyPem);

internal sealed class GitHubAppRegistration(IModuleIntegrations<GitHubModule> integrations)
{
    private const string AppIdField = "AppId";
    private const string PrivateKeyField = "PrivateKeyPem";

    private IIntegrationHandle Registration => integrations.Get("github");

    internal static long? AppIdOf(IntegrationSnapshot snapshot)
        => snapshot.Settings.TryGetValue(AppIdField, out var text) && long.TryParse(text, out var appId) && appId > 0 ? appId : null;

    internal async Task<long?> ReadAppIdAsync(CancellationToken cancellationToken)
    {
        var snapshot = await Registration.ReadAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Status == IntegrationStatus.Ready ? AppIdOf(snapshot) : null;
    }

    internal async Task<string?> ReadOptionalSecretAsync(string field, CancellationToken cancellationToken)
    {
        var snapshot = await Registration.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != IntegrationStatus.Ready) { return null; }
        try { return await Registration.ReadSecretAsync(field, cancellationToken).ConfigureAwait(false); }
        catch (KeyNotFoundException) { return null; }
    }

    internal async Task<GitHubAppCredentials> ReleaseAsync(CancellationToken cancellationToken)
    {
        var snapshot = await Registration.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != IntegrationStatus.Ready || AppIdOf(snapshot) is null)
        {
            throw new GitHubUnavailableException("The GitHub App is not registered for this deployment.");
        }

        var material = await Registration.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (AppIdOf(material.Snapshot) is not { } currentAppId)
        { throw new GitHubUnavailableException("The GitHub App registration is invalid."); }
        return new GitHubAppCredentials(currentAppId, material.Values[PrivateKeyField].Replace("\\n", "\n", StringComparison.Ordinal));
    }
}
