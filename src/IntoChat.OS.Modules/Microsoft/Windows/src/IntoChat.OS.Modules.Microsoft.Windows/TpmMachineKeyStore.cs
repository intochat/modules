using System.Runtime.Versioning;
using System.Security.Cryptography;
using IntoChat.OS.Framework.Machine;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// D-11: the machine key is a persisted, non-exportable ECDSA P-256 key in the Microsoft Platform Crypto Provider.
// Like the master keys it is per user: a host under another account cannot open it and sees the key as unavailable.
[SupportedOSPlatform("windows")]
internal sealed class TpmMachineKeyStore(string name = TpmMachineKeyStore.DefaultName) : IMachineKeyStore
{
    public const string DefaultName = TpmMasterKeyCustody.DefaultPrefix + "machine-key";

    public string Custody => "tpm";

    public ValueTask EnsureKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var existing = Open();
        if (existing is not null) { return ValueTask.CompletedTask; }
        var parameters = new CngKeyCreationParameters
        {
            Provider = TpmMasterKeyCustody.Provider,
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Signing,
        };
        try { CngKey.Create(CngAlgorithm.ECDsaP256, name, parameters).Dispose(); }
        // Another host may have created it first; that key is kept.
        catch (CryptographicException) when (Exists()) { return ValueTask.CompletedTask; }
        catch (CryptographicException exception) { throw new MachineKeyUnavailableException(exception); }
        return ValueTask.CompletedTask;
    }

    public ValueTask<byte[]> PublicKeySpkiAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Open() ?? throw new MachineKeyUnavailableException();
        using var ecdsa = new ECDsaCng(key);
        return ValueTask.FromResult(ecdsa.ExportSubjectPublicKeyInfo());
    }

    public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Open() ?? throw new MachineKeyUnavailableException();
        using var ecdsa = new ECDsaCng(key);
        try { return ValueTask.FromResult(ecdsa.SignData(data.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)); }
        catch (CryptographicException exception) { throw new MachineKeyUnavailableException(exception); }
    }

    public ValueTask DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Open();
        key?.Delete();
        return ValueTask.CompletedTask;
    }

    private bool Exists()
    {
        try { return CngKey.Exists(name, TpmMasterKeyCustody.Provider); }
        catch (CryptographicException) { return false; }
    }

    // A key that exists but cannot be opened (another account, a locked TPM) is unavailable, never a crash.
    private CngKey? Open()
    {
        if (!Exists()) { return null; }
        try { return CngKey.Open(name, TpmMasterKeyCustody.Provider); }
        catch (CryptographicException exception) { throw new MachineKeyUnavailableException(exception); }
    }
}
