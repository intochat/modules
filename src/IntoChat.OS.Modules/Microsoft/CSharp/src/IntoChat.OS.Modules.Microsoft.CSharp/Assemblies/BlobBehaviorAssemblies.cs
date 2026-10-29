using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;

// Content addressed: the blob name is the assembly's SHA-256, so a thousand installs of one app share one blob.
internal sealed class BlobBehaviorAssemblies(BlobServiceClient storage) : IBehaviorAssemblies
{
    public const string ContainerName = "behaviors";
    private readonly BlobContainerClient _container = storage.GetBlobContainerClient(ContainerName);

    public async Task Put(CompiledBehavior behavior, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _container.GetBlobClient(behavior.Hash).UploadAsync(new BinaryData(behavior.Assembly), overwrite: true, cancellationToken);
        // Written last: the symbols blob existing means the pair is complete, even for an assembly an older writer left alone.
        await _container.GetBlobClient(behavior.Hash + ".pdb").UploadAsync(new BinaryData(behavior.Symbols), overwrite: true, cancellationToken);
    }

    public async Task<bool> Exists(string hash, CancellationToken cancellationToken)
        => await _container.GetBlobClient(hash + ".pdb").ExistsAsync(cancellationToken);

    public async Task<Uri> ReadUrl(string name, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(name);
        var expires = DateTimeOffset.UtcNow + lifetime;
        // Azurite and a connection-string client sign with the account key; a managed identity needs a user delegation key.
        if (blob.CanGenerateSasUri) { return blob.GenerateSasUri(BlobSasPermissions.Read, expires); }
        var key = await storage.GetUserDelegationKeyAsync(new BlobGetUserDelegationKeyOptions(expires) { StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5) }, cancellationToken);
        return blob.GenerateUserDelegationSasUri(BlobSasPermissions.Read, expires, key.Value);
    }
}
