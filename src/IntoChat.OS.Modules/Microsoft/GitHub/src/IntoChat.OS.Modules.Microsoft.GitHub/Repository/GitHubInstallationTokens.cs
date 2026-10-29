using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

internal sealed class GitHubInstallationTokens : IGitHubInstallationVerifier, IDisposable
{
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Slot> _slots = new(StringComparer.Ordinal);

    private readonly GitHubAppRegistration _registration;
    private readonly int _capacity;

    internal const int DefaultCapacity = 32;

    public GitHubInstallationTokens(GitHubAppRegistration registration) : this(registration, null, null) { }
    internal GitHubInstallationTokens(GitHubAppRegistration registration, HttpMessageHandler? handler, TimeProvider? time, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _registration = registration;
        _capacity = capacity;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 65536 };
        _time = time ?? TimeProvider.System;
    }

    internal int CachedConnections => _slots.Count;

    // A disconnected or revoked connection's token leaves the cache with it.
    public void Forget(string connectionKey)
    {
        if (_slots.TryRemove(connectionKey, out var slot)) { slot.Token = null; }
    }

    // Slots are keyed by the connection key; a slot minted for other coordinates (a re-grant to
    // another installation or repository) is never reused.
    internal async Task<string> GetTokenAsync(GitHubRepository repository, bool refresh, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository.Key);
        var slot = SlotFor(repository.Key);
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!refresh && slot.Repository == repository && slot.Token is not null && slot.Expiry > _time.GetUtcNow().AddMinutes(2))
            {
                return slot.Token;
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(repository.ApiHost,
                $"app/installations/{repository.InstallationId}/access_tokens"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await CreateAppJwtAsync(repository, cancellationToken).ConfigureAwait(false));
            AddHeaders(request);
            request.Content = JsonContent.Create(new
            {
                repository_ids = new[] { repository.RepositoryId },
                permissions = new { contents = "read", pull_requests = "read", checks = "read", statuses = "read", metadata = "read", administration = "read" },
            });
            // This authentication POST is never retried; a later independent read can request a new token.
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            // A rate limit answers 403 or 429; it is transient and must not read as a denied App.
            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || response.StatusCode == HttpStatusCode.Forbidden && (response.Headers.RetryAfter is not null
                    || response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0")))
            {
                throw new GitHubUnavailableException("GitHub App authentication is rate limited. Retry later.");
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                throw new GitHubAccessDeniedException("GitHub App authentication was refused. Verify the installation, repository access and App permissions.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubUnavailableException("GitHub App authentication is temporarily unavailable.");
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
            var json = document.RootElement;
            var token = json.GetProperty("token").GetString();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 4096 || token.Any(char.IsControl)
                || !json.GetProperty("expires_at").TryGetDateTimeOffset(out var expiry) || expiry <= _time.GetUtcNow().AddMinutes(2))
            {
                throw new GitHubAccessDeniedException("GitHub returned invalid installation credentials.");
            }
            if (json.TryGetProperty("repositories", out var repositories)
                && (repositories.ValueKind != JsonValueKind.Array || repositories.GetArrayLength() != 1
                    || repositories[0].GetProperty("id").GetInt64() != repository.RepositoryId))
            {
                throw new GitHubAccessDeniedException("GitHub did not scope the installation token to the configured repository.");
            }
            if (!json.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Object
                || permissions.EnumerateObject().Any(static permission => permission.Value.GetString() != "read"))
            {
                throw new GitHubAccessDeniedException("GitHub did not grant a read-only installation token.");
            }
            slot.Token = token; slot.Expiry = expiry; slot.Repository = repository;
            return token;
        }
        catch (Exception error) when (error is not GitHubAccessDeniedException && (error is HttpRequestException or JsonException or CryptographicException or InvalidOperationException or KeyNotFoundException or ArgumentException))
        {
            throw new GitHubUnavailableException("GitHub App authentication could not be completed. Check the configured App key and connection.");
        }
        finally { slot.Gate.Release(); }
    }

    // At capacity the cache evicts, it never refuses: expired or never-minted slots first, then the
    // least recently used. A caller still holding an evicted slot finishes with it undisturbed.
    private Slot SlotFor(string key)
    {
        var now = _time.GetUtcNow();
        if (!_slots.ContainsKey(key) && _slots.Count >= _capacity)
        {
            foreach (var (stale, entry) in _slots)
            {
                if (entry.Token is null || entry.Expiry <= now) { _slots.TryRemove(stale, out _); }
            }
            while (_slots.Count >= _capacity && _slots.OrderBy(pair => pair.Value.LastUsed).Select(pair => pair.Key).FirstOrDefault() is { } oldest)
            {
                _slots.TryRemove(oldest, out _);
            }
        }
        var slot = _slots.GetOrAdd(key, static _ => new Slot());
        slot.LastUsed = now;
        return slot;
    }

    private async Task<string> CreateAppJwtAsync(GitHubRepository repository, CancellationToken cancellationToken)
    {
        var app = await _registration.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (app.AppId != repository.AppId)
        {
            throw new GitHubUnavailableException("The repository connection belongs to a different GitHub App than the registered one.");
        }

        return CreateAppJwt(app, _time.GetUtcNow());
    }

    internal static string CreateAppJwt(GitHubAppCredentials app, DateTimeOffset now)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = app.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(app.PrivateKeyPem);
        var message = $"{header}.{payload}";
        return $"{message}.{Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }

    public async Task VerifyAsync(GitHubRepository repository, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(repository.ApiHost, repository.RepositoryPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(repository, false, ct).ConfigureAwait(false));
        AddHeaders(request);
        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubAccessDeniedException("The connected GitHub repository could not be verified.");
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false));
            var repo = document.RootElement;
            if (repo.GetProperty("id").GetInt64() != repository.RepositoryId
                || !string.Equals(repo.GetProperty("name").GetString(), repository.Name, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(repo.GetProperty("owner").GetProperty("login").GetString(), repository.Owner, StringComparison.OrdinalIgnoreCase))
            {
                throw new GitHubAccessDeniedException("The repository was renamed or transferred. Reconnect GitHub.");
            }
        }
        catch (Exception error) when (error is not GitHubAccessDeniedException && (error is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException))
        {
            throw new GitHubUnavailableException("The connected GitHub repository could not be verified.");
        }
    }

    internal static void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.UserAgent.ParseAdd("IntoChatOS/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public void Dispose()
    {
        _http.Dispose();
        foreach (var slot in _slots.Values)
        {
            slot.Token = null;
            slot.Gate.Dispose();
        }
        _slots.Clear();
    }
    private sealed class Slot
    {
        internal readonly SemaphoreSlim Gate = new(1);
        internal GitHubRepository? Repository;
        internal string? Token;
        internal DateTimeOffset Expiry;
        internal DateTimeOffset LastUsed;
    }
}
