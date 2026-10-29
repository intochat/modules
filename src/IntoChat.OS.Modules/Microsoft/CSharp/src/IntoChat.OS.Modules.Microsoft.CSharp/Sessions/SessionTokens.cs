using System.Security.Cryptography;
using System.Text.Json;
using DigitalBrain.Runtime.Orleans;
using IntoChat.OS.Connections.Platform.Integrations;
using OsNames = IntoChat.OS.Contracts.Configuration.IntoChatOSNames;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sessions;

internal sealed record SessionClaims(string Brain, string Session, DateTimeOffset Expires);

// A sandbox session's bearer token for the runtime edge: the brain and session it speaks for, signed
// with HMAC-SHA256. The brain rotates it over the stream before it expires.
// A configured RunTokenKey wins. Development otherwise signs with a key made at startup.
// Local production (Topology local or local+cloud, including an unset topology) mints one key under
// <Home>/secrets and reuses it. Cloud production still requires the shared RunTokenKey.
internal sealed class SessionTokens(IModuleIntegrations<CSharpModule> integrations, TimeProvider time, IHostEnvironment environment, IConfiguration configuration, ILogger<SessionTokens> logger) : IIntegrationStartup
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    internal const string KeyFileName = "session-token.key";

    private byte[]? _key;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var handle = integrations.Get("csharp");
        var status = await handle.ReadAsync(cancellationToken);
        _key = status.Status == IntegrationStatus.Ready
            ? Configured(await handle.ReadSecretAsync("RunTokenKey", cancellationToken))
            : environment.IsDevelopment()
                ? Ephemeral(logger)
                : IsLocal(configuration)
                    ? LoadOrCreate(configuration, logger)
                    : throw new InvalidOperationException("The csharp Integration RunTokenKey must be provisioned before production startup.");
    }

    private static byte[] Configured(string secret)
    {
        var key = Convert.FromBase64String(secret);
        if (key.Length < 32) { throw new InvalidOperationException("The csharp Integration RunTokenKey must contain at least 32 bytes."); }
        return key;
    }

    private static byte[] Ephemeral(ILogger logger)
    {
        logger.LogWarning("The csharp Integration is unprovisioned; generated development session-token key. Tokens will not survive restart.");
        return RandomNumberGenerator.GetBytes(32);
    }

    private static bool IsLocal(IConfiguration configuration)
    {
        var topology = configuration[OsNames.TopologyKey];
        return string.IsNullOrWhiteSpace(topology)
            || string.Equals(topology, OsNames.TopologyLocal, StringComparison.OrdinalIgnoreCase)
            || string.Equals(topology, OsNames.TopologyLocalCloud, StringComparison.OrdinalIgnoreCase)
            || string.Equals(topology, OsNames.TopologyLocalCloudAlias, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] LoadOrCreate(IConfiguration configuration, ILogger logger)
    {
        var home = StateHome.Resolve(configuration).EnsureCreated();
        var path = Path.Combine(home.Secrets, KeyFileName);
        var protector = MachineKeyProtectors.ForCurrentPlatform();
        if (File.Exists(path))
        {
            var stored = protector.Unprotect(File.ReadAllBytes(path));
            if (stored.Length < 32)
            {
                throw new InvalidOperationException($"The session-token key under {home.Secrets} must contain at least 32 bytes.");
            }
            return stored;
        }

        var created = RandomNumberGenerator.GetBytes(32);
        Write(path, protector.Protect(created));
        logger.LogInformation("Generated a local session-token key under {Home}.", home.Path);
        return created;
    }

    private static void Write(string path, byte[] content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) { options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
        try
        {
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temp)) { File.Delete(temp); }
        }
    }

    private byte[] Key => _key ?? throw new InvalidOperationException("Session-token credentials have not completed Integration startup.");

    public string Issue(string brain, string session)
    {
        var claims = JsonSerializer.SerializeToUtf8Bytes(new SessionClaims(brain, session, time.GetUtcNow() + Lifetime), JsonSerializerOptions.Web);
        return Base64Url(claims) + "." + Base64Url(HMACSHA256.HashData(Key, claims));
    }

    public SessionClaims? Validate(string? token)
    {
        if (token?.Split('.') is not [var payload, var signature]) { return null; }
        byte[] claims;
        try { claims = FromBase64Url(payload); }
        catch (FormatException) { return null; }
        byte[] presented;
        try { presented = FromBase64Url(signature); }
        catch (FormatException) { return null; }
        if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(Key, claims), presented)) { return null; }
        var parsed = JsonSerializer.Deserialize<SessionClaims>(claims, JsonSerializerOptions.Web);
        return parsed is not null && parsed.Expires > time.GetUtcNow() ? parsed : null;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }
}
