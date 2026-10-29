using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Operations;
using DigitalBrain.Spi;
using IntoChat.OS.Framework.Diagnostics;
using IntoChat.OS.Runtime.Storage.Confinement;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.LocalDisk;

internal sealed record LocalRoot(string Id, string Path);

// storage@1 for allowed folders on this computer. Item ids are ld: plus root id and a relative path.
// Access recording belongs to the runtime broker, not this type.
internal sealed class LocalDiskStorage(IReadOnlyList<LocalRoot> roots, IOperations? operations = null, IVolumeSource? volumes = null) : IStorage
{
    internal const string TrashName = ".localdisk-trash";
    internal const string PartialSuffix = ".intochat-partial";
    private const string Label = "This computer";
    private const string Kind = "local-disk";
    private const int SearchDepth = 6;
    private const int SearchCap = 500;
    private const ItemFlags Offered = ItemFlags.Read | ItemFlags.Search | ItemFlags.Write | ItemFlags.CreateFolder
        | ItemFlags.Copy | ItemFlags.Move | ItemFlags.Rename | ItemFlags.Trash | ItemFlags.Delete;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private string[] Paths => [.. roots.Select(root => root.Path)];

    public Task<StorageDescriptor> Describe(CancellationToken ct = default)
    {
        var quota = roots.Count == 0 ? Quota.Unknown : LocalVolumes.QuotaFor(roots[0].Path, volumes ?? DriveVolumeSource.Instance);
        return Task.FromResult(new StorageDescriptor(Label, Kind, quota, Offered));
    }

    public Task<IReadOnlyList<Item>> Roots(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Item>>([.. roots.Where(root => Directory.Exists(root.Path)).Select(root => ItemOf(root, ""))]);

    public Task<Page<Item>> List(ItemId folder, ListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = Full(folder);
        return Task.FromResult(Guard(folder, () =>
        {
            if (!Directory.Exists(path)) { throw File.Exists(path) ? new Unsupported(folder) : new NotFound(folder); }
            ct.ThrowIfCancellationRequested();
            var items = new DirectoryInfo(path).EnumerateFileSystemInfos().Where(entry => Visible(entry.Name)).Select(ItemOf);
            return Cut(Sorted(items, query.Sort, query.Descending), query.Cursor, query.PageSize);
        }));
    }

    public Task<Item> Stat(ItemId id, CancellationToken ct = default)
    {
        var path = Full(id);
        return Task.FromResult(Guard(id, () =>
            Directory.Exists(path) ? ItemOf(new DirectoryInfo(path))
            : File.Exists(path) ? ItemOf(new FileInfo(path))
            : throw new NotFound(id)));
    }

    public async Task<Content> Read(ItemId id, ByteRange? range, CancellationToken ct = default)
    {
        await using var stream = await OpenRead(id, range, ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
        return new Content(buffer.ToArray(), stream.ContentType, new FileInfo(Full(id)).Length);
    }

    public Task<Page<Item>> Search(SearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var starts = query.Within is { } within ? [Full(within)] : roots.Select(root => root.Path).Where(Directory.Exists).ToArray();
        return Task.FromResult(Guard(query.Within, () =>
        {
            var found = new List<Item>();
            foreach (var start in starts)
            {
                Walk(start, RelativeOf(start), 0, query.Text, found, ct);
                if (found.Count >= SearchCap) { break; }
            }
            return Cut(Sorted(found, DigitalBrain.Capabilities.Storage.SortKey.FolderThenName, descending: false), query.Cursor, query.PageSize);
        }));
    }

    public Task<IReadOnlyList<Item>> Ancestors(ItemId id, CancellationToken ct = default)
    {
        var path = Full(id);
        return Task.FromResult(Guard(id, () =>
        {
            if (!Directory.Exists(path) && !File.Exists(path)) { throw new NotFound(id); }
            var (rootId, relative) = LocalItemId.Read(id);
            var root = Root(rootId);
            var chain = new List<Item> { ItemOf(root, "") };
            var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var walked = "";
            for (var index = 0; index < parts.Length - 1; index++)
            {
                walked = walked.Length == 0 ? parts[index] : walked + "/" + parts[index];
                chain.Add(ItemOf(root, walked));
            }
            return relative.Length == 0 ? [] : (IReadOnlyList<Item>)chain;
        }));
    }

    public Task<StorageReadStream> OpenRead(ItemId item, ByteRange? range, CancellationToken ct = default)
    {
        var path = Full(item);
        return Task.FromResult(Guard(item, () =>
        {
            if (Directory.Exists(path)) { throw new Unsupported(item); }
            var file = new FileInfo(path);
            if (!file.Exists) { throw new NotFound(item); }
            Recheck(path, item);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var offset = Math.Min(range?.Offset ?? 0, file.Length);
            var length = range?.Length is { } wanted ? Math.Min(wanted, file.Length - offset) : file.Length - offset;
            stream.Seek(offset, SeekOrigin.Begin);
            Stream content = range is null ? stream : new BoundedStream(stream, length);
            return new StorageReadStream(content, length, ContentType(path), null, canSeek: stream.CanSeek, file.LastWriteTimeUtc);
        }));
    }

    public async Task<OperationId> Write(WritePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ct.ThrowIfCancellationRequested();
        var host = operations ?? throw new InvalidOperationException("Local disk writes need the host's operations.");
        var operation = await host.Start(new LocalDiskOperation(plan with { Inline = null }, plan.Inline?.Length,
            plan.Inline is { } bytes ? Convert.ToHexStringLower(SHA256.HashData(bytes.Span)) : null), ct).ConfigureAwait(false);
        _ = Task.Run(() => CompleteWrite(plan, operation, ct), CancellationToken.None);
        return operation.Id;
    }

    public Task<StorageWriteStream> OpenWrite(WritePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ct.ThrowIfCancellationRequested();
        if (plan.Kind != WriteKind.WriteFile || plan.DryRun) { throw new Unsupported(plan.TargetFolder); }
        return Task.FromResult(Guard(plan.TargetFolder, () =>
        {
            var destination = Prepare(plan).Destination ?? throw new Unsupported(plan.TargetFolder);
            destination = Place(destination, plan.Conflict, source: null, plan.TargetFolder);
            var stage = Partial(destination);
            Recheck(destination, plan.TargetFolder);
            PathConfinement.RequireNoLinks(Path.GetDirectoryName(destination)!);
            if (File.Exists(stage)) { File.Delete(stage); }
            var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
            return (StorageWriteStream)new WriteHandle(this, plan, stage, destination, stream);
        }));
    }

    private async Task CompleteWrite(WritePlan plan, IOperation operation, CancellationToken requested)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(requested, operation.Cancellation);
        var ct = cancellation.Token;
        OperationResult result;
        try
        {
            ct.ThrowIfCancellationRequested();
            var prepared = Prepare(plan);
            if (plan.DryRun) { result = new OperationResult.Planned([plan.Kind.ToString()]); }
            else
            {
                await operation.Report(0).ConfigureAwait(false);
                await Apply(plan, ct).ConfigureAwait(false);
                await operation.Report(1).ConfigureAwait(false);
                result = new OperationResult.Succeeded();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { result = new OperationResult.Cancelled(); }
        catch (StorageException error) { result = new OperationResult.Failed(error.Code, error.Message); }
        catch (UnauthorizedAccessException) { result = new OperationResult.Failed("denied", "Storage access was denied."); }
        catch (IOException) { result = new OperationResult.Failed("unavailable", "The storage change could not be completed."); }
        catch (ArgumentException) { result = new OperationResult.Failed("invalid-plan", "The storage plan is invalid."); }
        await operation.Complete(result).ConfigureAwait(false);
    }

    private (string? Source, string? Destination) Prepare(WritePlan plan)
    {
        if (!Enum.IsDefined(plan.Kind) || !Enum.IsDefined(plan.Conflict)) { throw new Unsupported(plan.Source); }
        string? source = plan.Source is { } item ? Full(item) : null;
        if (source is not null)
        {
            if (!Exists(source)) { throw new NotFound(plan.Source); }
            if (IsRoot(source)) { throw new Denied(plan.Source); }
            PathConfinement.RequireNoLinks(source);
        }
        string? destination = plan.Kind switch
        {
            WriteKind.CreateFolder or WriteKind.WriteFile => Path.Combine(Folder(plan.TargetFolder), Safe(plan.Name)),
            WriteKind.Copy or WriteKind.Move => source is null ? throw new Unsupported(null) : Path.Combine(Folder(plan.TargetFolder), Path.GetFileName(source)),
            WriteKind.Rename => source is null ? throw new Unsupported(null) : Path.Combine(Path.GetDirectoryName(source)!, Safe(plan.Name)),
            _ => null,
        };
        if (destination is not null)
        {
            Recheck(destination, plan.TargetFolder ?? plan.Source);
            if (source is not null && plan.Kind is WriteKind.Move or WriteKind.Copy && Inside(source, destination)) { throw new Denied(plan.Source); }
        }
        return (source, destination);
    }

    private async Task Apply(WritePlan plan, CancellationToken ct)
    {
        if (plan.Kind == WriteKind.WriteFile)
        {
            if (plan.Inline is not { } content) { throw new Unsupported(plan.TargetFolder); }
            await using var writer = await OpenWrite(plan, ct).ConfigureAwait(false);
            await writer.Content.WriteAsync(content, ct).ConfigureAwait(false);
            _ = await writer.Commit(ct).ConfigureAwait(false);
            return;
        }
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (source, destination) = Prepare(plan);
            ct.ThrowIfCancellationRequested();
            if (plan.Kind == WriteKind.Delete)
            {
                if (plan.Permanent) { DeletePath(source!); }
                else { Trash(source!); }
                return;
            }
            destination = Place(destination!, plan.Conflict, plan.Kind is WriteKind.Move or WriteKind.Rename ? source : null, plan.Source ?? plan.TargetFolder);
            if (plan.Kind == WriteKind.CreateFolder)
            {
                Directory.CreateDirectory(destination);
                return;
            }
            if (plan.Kind == WriteKind.Copy)
            {
                CopyPath(source!, destination);
                return;
            }
            MovePath(source!, destination, plan.Conflict == ConflictPolicy.Overwrite);
        }
        finally { _gate.Release(); }
    }

    private async Task<ItemId> Commit(string stage, string destination, WritePlan plan, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            destination = Place(destination, plan.Conflict, source: null, plan.TargetFolder);
            Recheck(destination, plan.TargetFolder);
            var overwrite = plan.Conflict == ConflictPolicy.Overwrite;
            if (!overwrite && Exists(destination)) { throw new Conflict(plan.TargetFolder); }
            File.Move(stage, destination, overwrite);
            var root = RootContaining(destination);
            return LocalItemId.Of(root.Id, Relative(root.Path, destination));
        }
        finally { _gate.Release(); }
    }

    private string Full(ItemId id)
    {
        var (rootId, relative) = LocalItemId.Read(id);
        var root = Root(rootId);
        var combined = relative.Length == 0 ? root.Path : Join(root.Path, relative);
        try
        {
            var full = PathConfinement.Resolve(Paths, combined);
            if (!Under(root.Path, full)) { throw new Denied(id); }
            if (Hidden(full)) { throw new NotFound(id); }
            return full;
        }
        catch (ConfinementDenied error) { throw new Denied(id, error); }
        catch (UnauthorizedAccessException error) { throw new Denied(id, error); }
    }

    private void Recheck(string path, ItemId? id)
    {
        try
        {
            var full = PathConfinement.Resolve(Paths, path);
            if (roots.All(root => !Under(root.Path, full))) { throw new Denied(id); }
        }
        catch (ConfinementDenied error) { throw new Denied(id, error); }
    }

    private string Folder(ItemId? id)
    {
        if (id is not { } folder) { throw new Unsupported(null); }
        var path = Full(folder);
        if (!Directory.Exists(path)) { throw Exists(path) ? new Unsupported(id) : new NotFound(id); }
        return path;
    }

    private static string Safe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') || name is "." or "..")
        { throw new ArgumentException("The storage plan is invalid."); }
        if (name.EndsWith(PartialSuffix, PathComparison) || name.Equals(TrashName, PathComparison)) { throw new Denied(null); }
        return name;
    }

    private string Place(string destination, ConflictPolicy policy, string? source, ItemId? id)
    {
        if (source is not null && string.Equals(source, destination, PathComparison)) { return destination; }
        if (!Exists(destination)) { return destination; }
        if (policy == ConflictPolicy.Overwrite) { return destination; }
        if (policy == ConflictPolicy.Fail) { throw new Conflict(id); }
        var directory = Path.GetDirectoryName(destination)!;
        var stem = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        for (var index = 2; index < 1000; index++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!Exists(candidate)) { return candidate; }
        }
        throw new Conflict(id);
    }

    private void Trash(string source)
    {
        var root = RootContaining(source);
        var trash = Path.Combine(root.Path, TrashName);
        Directory.CreateDirectory(trash);
        var target = Path.Combine(trash, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(source));
        MovePath(source, target, overwrite: false);
    }

    private static void DeletePath(string path)
    {
        if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); }
        else if (File.Exists(path)) { File.Delete(path); }
    }

    private static void CopyPath(string source, string destination)
    {
        if (File.Exists(source)) { File.Copy(source, destination, overwrite: false); return; }
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            CopyPath(entry.FullName, Path.Combine(destination, entry.Name));
        }
    }

    private static void MovePath(string source, string destination, bool overwrite)
    {
        if (overwrite && Exists(destination) && !string.Equals(source, destination, PathComparison)) { DeletePath(destination); }
        if (Directory.Exists(source)) { Directory.Move(source, destination); }
        else { File.Move(source, destination, overwrite: false); }
    }

    private static string Partial(string destination)
        => Path.Combine(Path.GetDirectoryName(destination)!, "." + Path.GetFileName(destination) + PartialSuffix);

    private void Walk(string directory, string relative, int depth, string text, List<Item> found, CancellationToken ct)
    {
        if (depth > SearchDepth || found.Count >= SearchCap) { return; }
        ct.ThrowIfCancellationRequested();
        IEnumerable<FileSystemInfo> entries;
        try { entries = new DirectoryInfo(directory).EnumerateFileSystemInfos().ToList(); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        foreach (var entry in entries)
        {
            if (!Visible(entry.Name) || entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) { continue; }
            ct.ThrowIfCancellationRequested();
            var child = relative.Length == 0 ? entry.Name : relative + "/" + entry.Name;
            if (entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase) && found.Count < SearchCap)
            {
                var root = RootContaining(entry.FullName);
                found.Add(ItemOf(root, child));
            }
            if (entry is DirectoryInfo && depth < SearchDepth) { Walk(entry.FullName, child, depth + 1, text, found, ct); }
        }
    }

    private Item ItemOf(FileSystemInfo entry)
    {
        var root = RootContaining(entry.FullName);
        var relative = Relative(root.Path, entry.FullName);
        var folder = entry is DirectoryInfo;
        var flags = folder
            ? IsRoot(entry.FullName) ? ItemFlags.Read | ItemFlags.Search | ItemFlags.Write | ItemFlags.CreateFolder : Offered
            : Offered & ~(ItemFlags.Search | ItemFlags.CreateFolder);
        long? size = entry is FileInfo file ? file.Length : null;
        return new Item(LocalItemId.Of(root.Id, relative), entry.Name, folder ? ItemKind.Folder : ItemKind.File, size, entry.LastWriteTimeUtc, flags);
    }

    private Item ItemOf(LocalRoot root, string relative)
    {
        var path = relative.Length == 0 ? root.Path : Join(root.Path, relative);
        var folder = Directory.Exists(path);
        var flags = relative.Length == 0
            ? ItemFlags.Read | ItemFlags.Search | ItemFlags.Write | ItemFlags.CreateFolder
            : folder ? Offered : Offered & ~(ItemFlags.Search | ItemFlags.CreateFolder);
        long? size = !folder && File.Exists(path) ? new FileInfo(path).Length : null;
        DateTimeOffset? modified = Directory.Exists(path) || File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        var name = relative.Length == 0 ? Name(root.Path) : Path.GetFileName(path);
        return new Item(LocalItemId.Of(root.Id, relative), name, folder ? ItemKind.Folder : ItemKind.File, size, modified, flags);
    }

    private string RelativeOf(string full)
    {
        var root = RootContaining(full);
        return Relative(root.Path, full);
    }

    private LocalRoot Root(string rootId) => roots.FirstOrDefault(root => root.Id == rootId) ?? throw new NotFound(null);

    private LocalRoot RootContaining(string full)
        => roots.FirstOrDefault(root => Under(root.Path, full)) ?? throw new Denied(null);

    private bool IsRoot(string path) => roots.Any(root => string.Equals(root.Path, PathConfinement.Normalize(path), PathComparison));

    private static bool Under(string root, string full)
    {
        if (string.Equals(root, full, PathComparison)) { return true; }
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, PathComparison);
    }

    private static bool Inside(string parent, string child)
    {
        if (string.Equals(parent, child, PathComparison)) { return true; }
        var prefix = parent.EndsWith(Path.DirectorySeparatorChar) ? parent : parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, PathComparison);
    }

    private static bool Hidden(string full)
    {
        foreach (var part in full.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals(TrashName, PathComparison) || part.EndsWith(PartialSuffix, PathComparison)) { return true; }
        }
        return false;
    }

    private static bool Visible(string name)
        => !name.Equals(TrashName, PathComparison) && !name.EndsWith(PartialSuffix, PathComparison);

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static string Join(string root, string relative)
    {
        var path = root;
        foreach (var part in relative.Split('/', StringSplitOptions.RemoveEmptyEntries)) { path = Path.Combine(path, part); }
        return path;
    }

    private static string Relative(string root, string full)
    {
        if (string.Equals(root, full, PathComparison)) { return ""; }
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string Name(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;

    private static string ContentType(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".TXT" or ".LOG" or ".MD" or ".CSV" => "text/plain",
        ".JSON" => "application/json",
        ".HTML" or ".HTM" => "text/html",
        ".PDF" => "application/pdf",
        ".PNG" => "image/png",
        ".JPG" or ".JPEG" => "image/jpeg",
        _ => "application/octet-stream",
    };

    private static List<Item> Sorted(IEnumerable<Item> items, DigitalBrain.Capabilities.Storage.SortKey sort, bool descending)
    {
        var ordered = sort switch
        {
            DigitalBrain.Capabilities.Storage.SortKey.Name => items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            DigitalBrain.Capabilities.Storage.SortKey.Modified => items.OrderBy(item => item.Modified),
            DigitalBrain.Capabilities.Storage.SortKey.Size => items.OrderBy(item => item.Size ?? -1),
            _ => items.OrderBy(item => item.Kind == ItemKind.Folder ? 0 : 1).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
        };
        var list = ordered.ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        if (descending) { list.Reverse(); }
        return list;
    }

    private static Page<Item> Cut(List<Item> all, string? cursor, int pageSize)
    {
        var offset = 0;
        if (cursor is { Length: > 0 })
        {
            try { offset = int.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), NumberStyles.None, CultureInfo.InvariantCulture); }
            catch (FormatException) { offset = 0; }
            catch (OverflowException) { offset = 0; }
            offset = Math.Clamp(offset, 0, all.Count);
        }
        var end = Math.Min(all.Count, offset + pageSize);
        return new Page<Item>([.. all.Skip(offset).Take(end - offset)],
            end < all.Count ? Convert.ToBase64String(Encoding.UTF8.GetBytes(end.ToString(CultureInfo.InvariantCulture))) : null);
    }

    private static T Guard<T>(ItemId? id, Func<T> body)
    {
        try { return body(); }
        catch (StorageException) { throw; }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { throw new NotFound(id, error); }
        catch (UnauthorizedAccessException error) { throw new Denied(id, error); }
        catch (IOException error) { throw new Unavailable(id, error); }
    }

    private void Cleanup(string stage)
    {
        try { if (File.Exists(stage)) { File.Delete(stage); } }
        catch (IOException error) { NullLogger.Instance.LogSwallowed(error, "Storage staging cleanup could not finish."); }
        catch (UnauthorizedAccessException error) { NullLogger.Instance.LogSwallowed(error, "Storage staging cleanup could not finish."); }
    }

    private sealed class WriteHandle(LocalDiskStorage owner, WritePlan plan, string stage, string destination, FileStream stream) : StorageWriteStream(stream)
    {
        protected override async Task<ItemId> CommitCore(CancellationToken ct)
        {
            try
            {
                await stream.FlushAsync(ct).ConfigureAwait(false);
                await stream.DisposeAsync().ConfigureAwait(false);
                return await owner.Commit(stage, destination, plan, ct).ConfigureAwait(false);
            }
            finally { owner.Cleanup(stage); }
        }

        protected override async Task AbortCore(CancellationToken ct)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            owner.Cleanup(stage);
        }
    }

    private sealed record LocalDiskOperation(WritePlan Plan, long? Bytes, string? Sha256) : OperationPlan(Plan.DryRun);

    private sealed class BoundedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _length;
        private long _left;

        public BoundedStream(Stream inner, long length)
        {
            _inner = inner;
            _length = length;
            _left = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_left <= 0) { return 0; }
            var read = _inner.Read(buffer, offset, (int)Math.Min(count, _left));
            _left -= read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_left <= 0) { return 0; }
            var read = await _inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _left)], cancellationToken).ConfigureAwait(false);
            _left -= read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { _inner.Dispose(); } }
        public override ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
