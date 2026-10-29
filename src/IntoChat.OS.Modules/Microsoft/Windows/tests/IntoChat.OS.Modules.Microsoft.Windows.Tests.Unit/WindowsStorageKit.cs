using DigitalBrain.Capabilities.Storage;
using IntoChat.OS.Runtime.Storage.Confinement;
using DigitalBrain.Testing.Conformance.Storage;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Unit;

// storage@1 of a Windows PC, against the neuron's storage over a fresh folder (CAP-04). Each fact gets its own root.
public sealed class WindowsStorageKit : StorageConformance<IStorage>
{
    private string? _root;

    protected override async Task<StorageSubject> Connect(StorageSeed seed, CancellationToken ct)
    {
        var root = PathConfinement.Normalize(Directory.CreateTempSubdirectory("windows-kit").FullName);
        _root = root;
        foreach (var entry in seed.Entries)
        {
            var path = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (entry.Kind == ItemKind.Folder)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, entry.Bytes.ToArray(), ct).ConfigureAwait(false);
            }
        }

        var operations = new InMemoryOperations();
        var storage = new WindowsStorage([root], operations: operations);
        var outside = PathConfinement.Normalize(Path.Combine(Directory.GetParent(root)!.FullName, "escape.txt"));
        var windows = PathConfinement.Normalize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "win.ini"));
        return new StorageSubject(storage, operations, [WindowsStorage.IdOf(outside), WindowsStorage.IdOf(windows)]);
    }

    protected override ItemId MissingId()
        => WindowsStorage.IdOf(Path.Combine(_root ?? throw new InvalidOperationException("Connect runs first."), $"missing-{Guid.NewGuid():N}.txt"));
}
