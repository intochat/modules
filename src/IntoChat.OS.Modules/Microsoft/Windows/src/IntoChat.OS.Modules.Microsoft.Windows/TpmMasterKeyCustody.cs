using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using IntoChat.OS.Framework.Parts;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// R35: each account's master key is a non-exportable RSA-2048 key in the TPM (Microsoft Platform Crypto Provider).
// A secret is sealed with a fresh AES-256-GCM data key (associated data = context) and that data key is wrapped by
// RSA-OAEP-SHA256 under the account's TPM key. Wire: wrapped data key (256) | nonce (12) | tag (16) | ciphertext.
// Keys are per user (no CngKeyCreationOptions.MachineKey): a host run under another account, such as a service
// account, sees none of them, so its part opens fail with the R74 "not unwrappable on this machine" error.
[SupportedOSPlatform("windows")]
internal sealed class TpmMasterKeyCustody(string prefix = TpmMasterKeyCustody.DefaultPrefix) : IMasterKeyCustody
{
    public const string DefaultPrefix = "intochat-os-";

    internal static readonly CngProvider Provider = new("Microsoft Platform Crypto Provider");
    private const int WrappedKeyBytes = 256;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int Header = WrappedKeyBytes + NonceBytes + TagBytes;

    // Creates and deletes a throwaway named key (the provider makes no ephemeral keys): opening the provider alone can
    // succeed on a system whose TPM cannot make keys.
    public static bool IsAvailable()
    {
        try
        {
            using var probe = CngKey.Create(CngAlgorithm.ECDsaP256, $"intochat-os-probe-{Guid.NewGuid():N}", new CngKeyCreationParameters { Provider = Provider });
            probe.Delete();
            return true;
        }
        catch (CryptographicException) { return false; }
        catch (PlatformNotSupportedException) { return false; }
    }

    public byte[] Wrap(string accountId, string context, byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(secret);
        using var key = OpenOrCreate(accountId, prefix);
        using var tpm = new RSACng(key);
        // Encrypting needs only the public half; doing it in software spares the TPM a round trip.
        using var wrapper = RSA.Create(tpm.ExportParameters(includePrivateParameters: false));
        var dataKey = RandomNumberGenerator.GetBytes(32);
        try
        {
            var output = new byte[Header + secret.Length];
            wrapper.Encrypt(dataKey, RSAEncryptionPadding.OaepSHA256).CopyTo(output, 0);
            var nonce = output.AsSpan(WrappedKeyBytes, NonceBytes);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(dataKey, TagBytes);
            aes.Encrypt(nonce, secret, output.AsSpan(Header), output.AsSpan(WrappedKeyBytes + NonceBytes, TagBytes), Encoding.UTF8.GetBytes(context));
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(dataKey); }
    }

    public byte[] Unwrap(string accountId, string context, byte[] wrapped)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(wrapped);
        if (wrapped.Length < Header) { throw new CryptographicException("The wrapped secret is truncated."); }
        using var key = Open(accountId, prefix) ?? throw new CryptographicException($"This machine's TPM holds no master key for '{accountId}'.");
        using var tpm = new RSACng(key);
        var dataKey = tpm.Decrypt(wrapped.AsSpan(0, WrappedKeyBytes).ToArray(), RSAEncryptionPadding.OaepSHA256);
        try
        {
            var secret = new byte[wrapped.Length - Header];
            using var aes = new AesGcm(dataKey, TagBytes);
            aes.Decrypt(wrapped.AsSpan(WrappedKeyBytes, NonceBytes), wrapped.AsSpan(Header), wrapped.AsSpan(WrappedKeyBytes + NonceBytes, TagBytes),
                secret, Encoding.UTF8.GetBytes(context));
            return secret;
        }
        finally { CryptographicOperations.ZeroMemory(dataKey); }
    }

    internal static CngKey OpenOrCreate(string accountId, string prefix = DefaultPrefix)
    {
        if (Open(accountId, prefix) is { } existing) { return existing; }
        var parameters = new CngKeyCreationParameters
        {
            Provider = Provider,
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Decryption,
        };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
        try { return CngKey.Create(CngAlgorithm.Rsa, NameOf(accountId, prefix), parameters); }
        catch (CryptographicException) when (Open(accountId, prefix) is { } raced) { return raced; }
    }

    internal static void Delete(string accountId, string prefix = DefaultPrefix) => Open(accountId, prefix)?.Delete();

    private static CngKey? Open(string accountId, string prefix) =>
        CngKey.Exists(NameOf(accountId, prefix), Provider) ? CngKey.Open(NameOf(accountId, prefix), Provider) : null;

    private static string NameOf(string accountId, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        return $"{prefix}{accountId}";
    }
}
