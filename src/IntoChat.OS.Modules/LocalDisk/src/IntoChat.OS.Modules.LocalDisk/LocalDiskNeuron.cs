using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Operations;
using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.LocalDisk;

public sealed class LocalDiskState
{
    public string[] RootIds { get; set; } = [];
    public string[] RootPaths { get; set; } = [];
}

[DigitalBrain.Signal("local-disk.roots")]
public sealed record LocalDiskRootsChanged(string Action) : DigitalBrain.Signal;

public interface ILocalDisk
{
    Task<string[]> Roots();

    Task AllowRoot(string path);

    Task RevokeRoot(string path);
}

// Local disks on this computer. Every call is confined; the filesystem work is LocalDiskStorage.
[DigitalBrain.Neuron("local-disk", State = "local-disk")]
public sealed class LocalDiskNeuron : DigitalBrain.Neuron<LocalDiskState>, ILocalDisk, IStorage
{
    internal const string TypeName = "local-disk";
    internal const string KeyName = "main";

    private LocalDiskStorage Engine => new(Current(), Context.Operations, DriveVolumeSource.Instance);

    public Task<string[]> Roots() => Task.FromResult(Paths());

    public async Task AllowRoot(string path)
    {
        var full = LocalDiskRoots.Prepare(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (State.RootPaths.Any(root => string.Equals(root, full, comparison))) { return; }
        await Save(new LocalDiskState
        {
            RootIds = [.. State.RootIds, Guid.NewGuid().ToString("N")],
            RootPaths = [.. State.RootPaths, full],
        }, new LocalDiskRootsChanged("allow")).ConfigureAwait(false);
    }

    public async Task RevokeRoot(string path)
    {
        var full = PathConfinement.Normalize(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var kept = State.RootPaths
            .Select((root, index) => (root, index))
            .Where(pair => !string.Equals(pair.root, full, comparison))
            .ToArray();
        await Save(new LocalDiskState
        {
            RootIds = [.. kept.Select(pair => State.RootIds[pair.index])],
            RootPaths = [.. kept.Select(pair => pair.root)],
        }, new LocalDiskRootsChanged("revoke")).ConfigureAwait(false);
    }

    public Task<StorageDescriptor> Describe(CancellationToken ct = default)
    {
        foreach (var path in Paths()) { _ = PathConfinement.Resolve(Paths(), path); }
        return Engine.Describe(ct);
    }

    public Task<IReadOnlyList<Item>> Roots(CancellationToken ct = default)
    {
        foreach (var path in Paths()) { _ = PathConfinement.Normalize(path); }
        return Engine.Roots(ct);
    }

    public Task<Page<Item>> List(ItemId folder, ListQuery query, CancellationToken ct = default)
    {
        _ = PathConfinement.Resolve(Paths(), PathOf(folder));
        return Engine.List(folder, query, ct);
    }

    public Task<Item> Stat(ItemId id, CancellationToken ct = default)
    {
        _ = PathConfinement.Containing(Paths(), PathOf(id));
        return Engine.Stat(id, ct);
    }

    public Task<Content> Read(ItemId id, ByteRange? range, CancellationToken ct = default)
    {
        _ = PathConfinement.Resolve(Paths(), PathOf(id));
        return Engine.Read(id, range, ct);
    }

    public Task<OperationId> Write(WritePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Source is { } source) { _ = PathConfinement.Resolve(Paths(), PathOf(source)); }
        if (plan.TargetFolder is { } folder) { _ = PathConfinement.Resolve(Paths(), PathOf(folder)); }
        return Engine.Write(plan, ct);
    }

    public Task<Page<Item>> Search(SearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        foreach (var path in Paths()) { _ = PathConfinement.Resolve(Paths(), path); }
        if (query.Within is { } within) { _ = PathConfinement.Resolve(Paths(), PathOf(within)); }
        return Engine.Search(query, ct);
    }

    public Task<IReadOnlyList<Item>> Ancestors(ItemId id, CancellationToken ct = default)
    {
        _ = PathConfinement.Containing(Paths(), PathOf(id));
        return Engine.Ancestors(id, ct);
    }

    public Task<StorageReadStream> OpenRead(ItemId item, ByteRange? range, CancellationToken ct = default)
    {
        _ = PathConfinement.Resolve(Paths(), PathOf(item));
        return Engine.OpenRead(item, range, ct);
    }

    // Stages .{name}.intochat-partial beside the destination, then File.Move(overwrite: false).
    public Task<StorageWriteStream> OpenWrite(WritePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var folder = plan.TargetFolder ?? throw new Unsupported(null);
        _ = PathConfinement.ResolveForCreate(Paths(), PathOf(folder), plan.Name ?? "file", PathConfinement.Current);
        return Engine.OpenWrite(plan, ct);
    }

    private LocalRoot[] Current()
    {
        var count = Math.Min(State.RootIds.Length, State.RootPaths.Length);
        var roots = new LocalRoot[count];
        for (var index = 0; index < count; index++) { roots[index] = new LocalRoot(State.RootIds[index], PathConfinement.Normalize(State.RootPaths[index])); }
        return roots;
    }

    private string[] Paths() => [.. Current().Select(root => root.Path)];

    private string PathOf(ItemId id)
    {
        var (rootId, relative) = LocalItemId.Read(id);
        var root = Current().FirstOrDefault(item => item.Id == rootId) ?? throw new NotFound(id);
        var combined = relative.Length == 0 ? root.Path : Join(root.Path, relative);
        return PathConfinement.Resolve(Paths(), combined);
    }

    private static string Join(string root, string relative)
    {
        var path = root;
        foreach (var part in relative.Split('/', StringSplitOptions.RemoveEmptyEntries)) { path = Path.Combine(path, part); }
        return path;
    }
}
