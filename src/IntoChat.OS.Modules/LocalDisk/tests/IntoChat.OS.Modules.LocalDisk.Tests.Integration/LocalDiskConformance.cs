using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Testing.Conformance.Storage;
using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.LocalDisk.Tests.Integration;

public sealed class LocalDiskConformance : StorageConformance<LocalDiskNeuron>
{
    private string? _rootId;

    protected override async Task<StorageSubject> Connect(StorageSeed seed, CancellationToken ct)
    {
        var root = PathConfinement.Normalize(Directory.CreateTempSubdirectory("localdisk-kit").FullName);
        foreach (var entry in seed.Entries)
        {
            var path = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (entry.Kind == ItemKind.Folder) { Directory.CreateDirectory(path); }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, entry.Bytes.ToArray(), ct).ConfigureAwait(false);
            }
        }
        var rootId = Guid.NewGuid().ToString("N");
        _rootId = rootId;
        var operations = new InMemoryOperations();
        var storage = new LocalDiskStorage([new LocalRoot(rootId, root)], operations);
        return new StorageSubject(storage, operations, [LocalItemId.Of(rootId, "../escape"), LocalItemId.Of(rootId, "C:/Windows/win.ini")]);
    }

    protected override ItemId MissingId()
        => LocalItemId.Of(_rootId ?? throw new InvalidOperationException("Connect runs first."), $"missing-{Guid.NewGuid():N}.txt");
}
