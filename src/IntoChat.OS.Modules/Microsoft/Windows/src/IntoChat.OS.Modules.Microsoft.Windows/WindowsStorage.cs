using IntoChat.OS.Runtime.Storage.Confinement;
using CultureInfo = System.Globalization.CultureInfo;
using NumberStyles = System.Globalization.NumberStyles;
using System.Text;
using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Spi;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// storage@1 of a Windows PC (05-08, D-03): the allowed roots, read fresh on every call. An ItemId is "w:" plus the base64url of
// the normalised full path; it is decoded only here and always resolved through PathConfinement, so an id outside the roots (an
// escape, a system file, a revoked root) is Denied. Streams stage on the destination volume and publish only on commit.
internal sealed partial class WindowsStorage(IReadOnlyList<string> roots, IVolumeProbe? volumes = null,
    IOperations? operations = null, Func<IReadOnlyList<string>>? currentRoots = null, WindowsStorageCoordinator? coordinator = null) : IStorage
{
    private readonly WindowsStorageCoordinator _coordinator = coordinator ?? new();
    internal const string IdPrefix = "w:";
    private const string Label = "This PC";
    private const string Kind = "windows";
    private const ItemFlags Offered = ItemFlags.Read | ItemFlags.Search | ItemFlags.Write | ItemFlags.CreateFolder
        | ItemFlags.Copy | ItemFlags.Move | ItemFlags.Rename | ItemFlags.Trash | ItemFlags.Delete;

    private string[] AllowedRoots => [.. currentRoots?.Invoke() ?? roots];

    public Task<StorageDescriptor> Describe(CancellationToken ct = default)
        => Task.FromResult(new StorageDescriptor(Label, Kind, LocalQuota.For(AllowedRoots, volumes ?? DriveInfoProbe.Instance), Offered));

    public Task<IReadOnlyList<Item>> Roots(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Item>>([.. AllowedRoots.Where(Directory.Exists).Select(root => ToItem(new DirectoryInfo(Resolve(IdOf(root)))))]);

    public Task<Page<Item>> List(ItemId folder, ListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = Resolve(folder);
        return Task.FromResult(Guard(folder, () =>
        {
            if (!Directory.Exists(path)) { throw File.Exists(path) ? new Unsupported(folder) : new NotFound(folder); }
            var directory = new DirectoryInfo(path);
            ct.ThrowIfCancellationRequested();
            var items = directory.EnumerateFileSystemInfos().Where(Visible).Select(ToItem);
            return Cut(Sorted(items, query.Sort, query.Descending), query.Cursor, query.PageSize);
        }));
    }

    public Task<Item> Stat(ItemId id, CancellationToken ct = default)
    {
        var path = Resolve(id);
        return Task.FromResult(Guard(id, () => Directory.Exists(path) ? ToItem(new DirectoryInfo(path))
            : File.Exists(path) ? ToItem(new FileInfo(path))
            : throw new NotFound(id)));
    }

    public async Task<Content> Read(ItemId id, ByteRange? range, CancellationToken ct = default)
    {
        await using var stream = await OpenRead(id, range, ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
        return new Content(buffer.ToArray(), stream.ContentType, new FileInfo(Resolve(id)).Length);
    }

    // Name contains across every descendant. The cursor pages results, never a silently truncated traversal.
    public Task<Page<Item>> Search(SearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var starts = query.Within is { } within ? [Resolve(within)] : AllowedRoots.Where(Directory.Exists).ToArray();
        return Task.FromResult(Guard(query.Within, () =>
        {
            var found = new List<Item>();
            var visited = new HashSet<string>(PathComparer);
            var pending = new Stack<DirectoryInfo>(starts.Select(start => new DirectoryInfo(start)).Reverse());
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                if (!visited.Add(directory.FullName)) { continue; }
                _ = Resolve(IdOf(directory.FullName));
                IEnumerable<FileSystemInfo> entries;
                // A folder the search may not enter (access denied, gone meanwhile) is passed over, not an error.
                try { entries = directory.EnumerateFileSystemInfos().ToList(); }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                foreach (var entry in entries.Where(Visible))
                {
                    ct.ThrowIfCancellationRequested();
                    if (entry.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase)) { found.Add(ToItem(entry)); }
                    if (entry is DirectoryInfo child && !child.Attributes.HasFlag(FileAttributes.ReparsePoint)) { pending.Push(child); }
                }
            }
            return Cut(Sorted(found, SortKey.FolderThenName, descending: false), query.Cursor, query.PageSize);
        }));
    }

    // The folders holding an item, its containing root first, excluding the item itself.
    public Task<IReadOnlyList<Item>> Ancestors(ItemId id, CancellationToken ct = default)
    {
        var path = Resolve(id);
        return Task.FromResult(Guard(id, () =>
        {
            if (!Directory.Exists(path) && !File.Exists(path)) { throw new NotFound(id); }
            var root = PathConfinement.Containing(AllowedRoots, path);
            var chain = new List<Item>();
            var current = root;
            foreach (var part in path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).SkipLast(1).Prepend(string.Empty))
            {
                current = part.Length == 0 ? current : Path.Combine(current, part);
                chain.Add(ToItem(new DirectoryInfo(current)));
            }
            return path.Equals(root, StringComparison.OrdinalIgnoreCase) ? [] : (IReadOnlyList<Item>)chain;
        }));
    }

    public Task<StorageReadStream> OpenRead(ItemId item, ByteRange? range, CancellationToken ct = default)
    {
        var path = Resolve(item);
        return Task.FromResult(Guard(item, () =>
        {
            if (Directory.Exists(path)) { throw new Unsupported(item); }
            var file = new FileInfo(path);
            if (!file.Exists) { throw new NotFound(item); }
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var offset = Math.Min(range?.Offset ?? 0, file.Length);
            var length = range?.Length is { } wanted ? Math.Min(wanted, file.Length - offset) : file.Length - offset;
            stream.Seek(offset, SeekOrigin.Begin);
            Stream content = range is null ? stream : new BoundedStream(stream, length);
            return new StorageReadStream(content, length, ContentType(path), ETag(file), canSeek: range is null, file.LastWriteTimeUtc);
        }));
    }

    // ----- ids -----

    internal static ItemId IdOf(string fullPath)
        => new(IdPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(fullPath)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));

    // Decodes an id and confines it to the roots: an id this module never issued is NotFound, anything outside the roots Denied.
    private string Resolve(ItemId id)
    {
        if (id.Value is not { } value || !value.StartsWith(IdPrefix, StringComparison.Ordinal)) { throw new NotFound(id); }
        string path;
        try
        {
            var text = value[IdPrefix.Length..].Replace('-', '+').Replace('_', '/');
            path = Encoding.UTF8.GetString(Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=')));
        }
        catch (FormatException) { throw new NotFound(id); }
        if (string.IsNullOrWhiteSpace(path)) { throw new NotFound(id); }
        try
        {
            var full = PathConfinement.Resolve(AllowedRoots, path);
            if (PrivatePath(full)) { throw new UnauthorizedAccessException("Storage working files and trash are private."); }
            foreach (var part in full[Path.GetPathRoot(full)!.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)) { _ = SafeName(part); }
            return full;
        }
        catch (UnauthorizedAccessException error) { throw new Denied(id, error); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { throw new Denied(id, error); }
    }

    // IO failures as typed storage errors: missing is NotFound, refused is Denied, a device not ready is Unavailable.
    private static T Guard<T>(ItemId? id, Func<T> body)
    {
        try { return body(); }
        catch (StorageException) { throw; }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { throw new NotFound(id, error); }
        catch (UnauthorizedAccessException error) { throw new Denied(id, error); }
        catch (IOException error) when (IoCode(error) == "quota-exceeded") { throw new QuotaExceeded(id, error); }
        catch (IOException error) { throw new Unavailable(id, error); }
    }

    // ----- items, sorting, paging -----

    private Item ToItem(FileSystemInfo entry) => entry switch
    {
        DirectoryInfo folder => new Item(IdOf(PathConfinement.Normalize(folder.FullName)), Name(folder.FullName), ItemKind.Folder, null, folder.LastWriteTimeUtc,
            IsRoot(folder.FullName) ? ItemFlags.Read | ItemFlags.Search | ItemFlags.Write | ItemFlags.CreateFolder : Offered),
        FileInfo file => new Item(IdOf(PathConfinement.Normalize(file.FullName)), file.Name, ItemKind.File, file.Length, file.LastWriteTimeUtc,
            Offered & ~(ItemFlags.Search | ItemFlags.CreateFolder)),
        _ => throw new ArgumentOutOfRangeException(nameof(entry)),
    };

    private static string Name(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;

    private static List<Item> Sorted(IEnumerable<Item> items, SortKey sort, bool descending)
    {
        var ordered = sort switch
        {
            SortKey.Name => items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            SortKey.Modified => items.OrderBy(item => item.Modified),
            SortKey.Size => items.OrderBy(item => item.Size ?? -1),
            _ => items.OrderBy(item => item.Kind == ItemKind.Folder ? 0 : 1).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
        };
        var list = ordered.ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        if (descending) { list.Reverse(); }
        return list;
    }

    // The cursor is an opaque offset into the sorted listing.
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

    private static string ETag(FileInfo file) => string.Create(CultureInfo.InvariantCulture, $"{file.LastWriteTimeUtc.Ticks:x}-{file.Length:x}");

    private static string ContentType(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".TXT" or ".LOG" or ".MD" or ".CSV" => "text/plain",
        ".JSON" => "application/json",
        ".HTML" or ".HTM" => "text/html",
        ".PDF" => "application/pdf",
        ".PNG" => "image/png",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".GIF" => "image/gif",
        _ => "application/octet-stream",
    };

    // A read-only window of length bytes over a stream already positioned at its start.
    private sealed class BoundedStream(Stream inner, long length) : Stream
    {
        private readonly long _length = length;
        private long _remaining = length;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _length - _remaining; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_remaining <= 0) { return 0; }
            var read = inner.Read(buffer[..(int)Math.Min(buffer.Length, _remaining)]);
            _remaining -= read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0) { return 0; }
            var read = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken).ConfigureAwait(false);
            _remaining -= read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
