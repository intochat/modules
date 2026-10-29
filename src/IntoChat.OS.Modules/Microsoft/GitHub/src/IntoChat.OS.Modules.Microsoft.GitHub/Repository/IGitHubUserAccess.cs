using System.Net.Http.Headers;
using System.Text.Json;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

// The user's token proves the intersection of user access and App installation access, once, while
// the grant is accepted; ongoing observation uses repository-scoped installation tokens.
internal interface IGitHubUserAccess
{
    // The returned coordinates carry an empty Key; the connection neuron fills in its own.
    Task<GitHubRepository> ResolveAsync(string userToken, long appId, string repositoryUrl, CancellationToken ct);
}

internal interface IGitHubInstallationVerifier
{
    Task VerifyAsync(GitHubRepository repository, CancellationToken ct);

    // Drops whatever the verifier cached for a connection that disconnected or was revoked.
    void Forget(string connectionKey);
}

internal sealed class GitHubUserAccess(IHttpClientFactory clients) : IGitHubUserAccess
{
    public const string HttpClientName = "IntoChat.OS.GitHub.UserAccess";

    public async Task<GitHubRepository> ResolveAsync(string userToken, long appId, string repositoryUrl, CancellationToken ct)
    {
        var coordinates = ParseUrl(repositoryUrl);
        var expected = $"{coordinates.Owner}/{coordinates.Name}";
        using var client = clients.CreateClient(HttpClientName);
        for (var page = 1; page <= 100; page++)
        {
            using var installations = await Read(client, userToken, $"user/installations?per_page=100&page={page}", ct).ConfigureAwait(false);
            var items = installations.RootElement.GetProperty("installations");
            foreach (var installation in items.EnumerateArray())
            {
                if (installation.GetProperty("app_id").GetInt64() != appId
                    || installation.TryGetProperty("suspended_at", out var suspension) && suspension.ValueKind != JsonValueKind.Null)
                {
                    continue;
                }
                var installationId = installation.GetProperty("id").GetInt64();
                for (var repoPage = 1; repoPage <= 100; repoPage++)
                {
                    using var repositories = await Read(client, userToken, $"user/installations/{installationId}/repositories?per_page=100&page={repoPage}", ct).ConfigureAwait(false);
                    var repos = repositories.RootElement.GetProperty("repositories");
                    foreach (var repository in repos.EnumerateArray())
                    {
                        if (string.Equals(repository.GetProperty("full_name").GetString(), expected, StringComparison.OrdinalIgnoreCase))
                        {
                            return new("", appId, installationId, repository.GetProperty("id").GetInt64(),
                                repository.GetProperty("owner").GetProperty("login").GetString()!, repository.GetProperty("name").GetString()!);
                        }
                    }
                    if (repos.GetArrayLength() < 100)
                    {
                        break;
                    }
                }
            }
            if (items.GetArrayLength() < 100)
            {
                break;
            }
        }
        throw new GitHubUnavailableException("The user and GitHub App must both have access to the requested repository. Install or configure the App, then connect again.");
    }

    internal static (string Owner, string Name) ParseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new GitHubUnavailableException("Use the HTTPS GitHub repository URL, for example https://github.com/intochat/intochat-os.");
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || parts.Any(part => part.Length == 0
            || part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            throw new GitHubUnavailableException("Choose one GitHub repository: a URL with an owner and repository name.");
        }

        return (parts[0], parts[1].EndsWith(".git", StringComparison.Ordinal) ? parts[1][..^4] : parts[1]);
    }

    private static async Task<JsonDocument> Read(HttpClient client, string token, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.github.com/" + path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("IntoChatOS/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubUnavailableException("GitHub repository authorization could not be verified.");
        }
        await response.Content.LoadIntoBufferAsync(4 * 1024 * 1024, ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
    }
}
