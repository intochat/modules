using System.Security.Cryptography;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Integration;

// Live-gated: these run against this PC's TPM through the Microsoft Platform Crypto Provider and skip where there is none.
// The class is Windows-only for the analyzer; each fact still skips first where the provider is missing.
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class TpmCustodyFacts
{
    [Fact]
    public void AWrappedSecretUnwrapsThroughTheSameTpmKey()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() && TpmMasterKeyCustody.IsAvailable(), "No TPM (Microsoft Platform Crypto Provider) on this machine.");
        var account = $"test-{Guid.NewGuid():N}";
        var custody = new TpmMasterKeyCustody();
        try
        {
            var secret = RandomNumberGenerator.GetBytes(32);
            var wrapped = custody.Wrap(account, "part|x", secret);

            Assert.Equal(secret, custody.Unwrap(account, "part|x", wrapped));
            Assert.ThrowsAny<CryptographicException>(() => { custody.Unwrap(account, "part|y", wrapped); });
            Assert.ThrowsAny<CryptographicException>(() => { custody.Unwrap($"other-{Guid.NewGuid():N}", "part|x", wrapped); });
        }
        finally { TpmMasterKeyCustody.Delete(account); }
    }

    [Fact]
    public void TheTpmKeyIsNotExportable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() && TpmMasterKeyCustody.IsAvailable(), "No TPM (Microsoft Platform Crypto Provider) on this machine.");
        var account = $"test-{Guid.NewGuid():N}";
        try
        {
            using var key = TpmMasterKeyCustody.OpenOrCreate(account);
            using var rsa = new RSACng(key);

            Assert.ThrowsAny<CryptographicException>(() => rsa.ExportParameters(includePrivateParameters: true));
            Assert.Equal(2048, rsa.KeySize);
        }
        finally { TpmMasterKeyCustody.Delete(account); }
    }

}
