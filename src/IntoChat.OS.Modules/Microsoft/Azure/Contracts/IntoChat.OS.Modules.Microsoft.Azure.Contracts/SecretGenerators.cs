using System.Globalization;
using System.Security.Cryptography;

namespace IntoChat.OS.Modules.Microsoft.Azure;

// Value generators for SecretSpec.Generator: "base64:<bytes>" is that many random bytes, base64 encoded;
// "p256-pkcs8" is a fresh ECDSA P-256 private key (PKCS#8, base64) for signing.
public static class SecretGenerators
{
    public const string P256Pkcs8 = "p256-pkcs8";

    private const string Base64Prefix = "base64:";

    public static string Base64(int bytes) => Base64Prefix + bytes.ToString(CultureInfo.InvariantCulture);

    public static string Generate(string generator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generator);
        if (generator == P256Pkcs8)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        }
        if (generator.StartsWith(Base64Prefix, StringComparison.Ordinal)
            && int.TryParse(generator.AsSpan(Base64Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) && bytes is > 0 and <= 1024)
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));
        }
        throw new ArgumentException($"Unknown secret generator '{generator}'.", nameof(generator));
    }

    // The SubjectPublicKeyInfo (base64) of a P-256 PKCS#8 private key made by P256Pkcs8.
    public static string P256PublicKey(string privateKeyPkcs8Base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPkcs8Base64);
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyPkcs8Base64), out _);
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }
}
