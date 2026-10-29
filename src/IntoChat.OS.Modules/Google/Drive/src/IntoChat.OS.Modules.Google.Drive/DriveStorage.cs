using CultureInfo = System.Globalization.CultureInfo;
using NumberStyles = System.Globalization.NumberStyles;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Operations;

namespace IntoChat.OS.Modules.Google.Drive;

// storage@1 of a Google Drive (05-09, D-06, D-11, D-16): ids are Drive file ids ("root" is My Drive, resolved here), lists
// follow nextPageToken (no 200-item truncation), quota is tri-state and pooled, shortcuts are links to their target (a folder
// shortcut lists its target; chains are not followed). Mutations are offered only when the stored OAuth grant allows them.
internal sealed partial class DriveNeuron : IStorage
{
    internal const string ShortcutType = "application/vnd.google-apps.shortcut";
    private const string GooglePrefix = "application/vnd.google-apps.";
    private ItemFlags Offered => ItemFlags.Read | ItemFlags.Search | (CanWrite
        ? ItemFlags.Write | ItemFlags.CreateFolder | ItemFlags.Rename | ItemFlags.Copy | ItemFlags.Move | ItemFlags.Trash | ItemFlags.Delete : ItemFlags.None);
    private const string ItemFields = "id,name,mimeType,size,modifiedTime,md5Checksum,trashed,parents,shortcutDetails,capabilities(canTrash,canDelete,canModifyContent,canRename,canMoveItemWithinDrive,canMoveItemOutOfDrive,canCopy,canAddChildren,canListChildren)";

    async Task<StorageDescriptor> IStorage.Describe(CancellationToken ct)
    {
        Quota quota;
        try
        {
            quota = await Storage(GoogleDriveOperations.About, "about?fields=storageQuota(limit,usage,usageInDrive,usageInDriveTrash)", null, ct,
                async response => DriveQuota.From(await response.Content.ReadAsStringAsync(ct)));
        }
        catch (StorageException) { quota = Quota.Unknown; }
        catch (HttpRequestException) { quota = Quota.Unknown; }
        return new StorageDescriptor("Google Drive", "google-drive", quota, Offered);
    }

    async Task<IReadOnlyList<Item>> IStorage.Roots(CancellationToken ct)
        => [await ((IStorage)this).Stat(new(Root), ct)];

    async Task<Page<Item>> IStorage.List(ItemId folder, ListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parent = await ListableFolder(folder, ct);
        var q = $"'{Escape(parent)}' in parents and trashed = false";
        return await Page(GoogleDriveOperations.List, q, OrderBy(query.Sort, query.Descending), query.PageSize, query.Cursor, folder, ct);
    }

    async Task<Item> IStorage.Stat(ItemId id, CancellationToken ct)
    {
        var file = await Metadata(id, ct);
        // My Drive answers with its real id; callers keep the alias they asked for.
        return id.Value == Root ? new Item(new ItemId(Root), RootName, ItemKind.Folder, null, ToItem(file).Modified, Flags(file)) : ToItem(file);
    }

    async Task<Content> IStorage.Read(ItemId id, ByteRange? range, CancellationToken ct)
    {
        var (bytes, mediaType, total) = await ReadBytes(id, range, ct);
        return new Content(bytes, mediaType, total);
    }

    async Task<Page<Item>> IStorage.Search(SearchQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var folder = query.Within is { } within ? await ListableFolder(within, ct) : null;
        var q = $"name contains '{Escape(query.Text)}' and trashed = false";
        var page = await Page(GoogleDriveOperations.List, q, OrderBy(SortKey.FolderThenName, false), query.PageSize, query.Cursor, query.Within, ct);
        if (folder is null) { return page; }
        var found = new List<Item>();
        foreach (var item in page.Items)
        {
            var parents = await ((IStorage)this).Ancestors(item.Id, ct);
            if (parents.Any(parent => parent.Id.Value == folder)) { found.Add(item); }
        }
        return new(found, page.NextCursor);
    }

    // The folders holding an item, My Drive first, excluding the item; parents[0] at each step, at most MaxDepth deep.
    async Task<IReadOnlyList<Item>> IStorage.Ancestors(ItemId id, CancellationToken ct)
    {
        if (id.Value is null or Root) { return []; }
        var rootId = (await Metadata(new ItemId(Root), ct)).GetProperty("id").GetString();
        var file = await Metadata(id, ct);
        var chain = new List<Item>();
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (!file.TryGetProperty("parents", out var parents) || parents.ValueKind != JsonValueKind.Array || parents.GetArrayLength() == 0) { break; }
            var parentId = parents[0].GetString()!;
            if (parentId == rootId)
            {
                chain.Add(new Item(new ItemId(Root), RootName, ItemKind.Folder, null, null, ItemFlags.Read));
                break;
            }
            file = await Metadata(new ItemId(parentId), ct);
            chain.Add(ToItem(file));
        }
        chain.Reverse();
        return chain;
    }

    async Task<StorageReadStream> IStorage.OpenRead(ItemId item, ByteRange? range, CancellationToken ct)
    {
        var file = await Metadata(item, ct);
        var type = file.GetProperty("mimeType").GetString() ?? "";
        if (type == FolderType || type == ShortcutType) { throw new Unsupported(item); }
        var modified = ToItem(file).Modified;
        var hash = file.TryGetProperty("md5Checksum", out var md5) ? md5.GetString() : null;
        if (type.StartsWith(GooglePrefix, StringComparison.Ordinal))
        {
            // Google documents stream their text export; other Google formats have none.
            var (bytes, mediaType, _) = await ReadBytes(item, range, ct);
            return new StorageReadStream(new MemoryStream(bytes, writable: false), bytes.Length, mediaType, hash, canSeek: false, modified);
        }
        var path = $"files/{Uri.EscapeDataString(item.Value!)}?alt=media";
        var response = await OpenStorage(GoogleDriveOperations.Media, path, item, range, ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        if (range is not null && response.StatusCode != HttpStatusCode.PartialContent)
        {
            // The server ignored Range and sent the whole file: cut the range here.
            using (response)
            {
                var cut = Slice(await response.Content.ReadAsByteArrayAsync(ct), range);
                return new StorageReadStream(new MemoryStream(cut, writable: false), cut.Length, contentType, hash, canSeek: false, modified);
            }
        }
        return new StorageReadStream(new ResponseStream(response, await response.Content.ReadAsStreamAsync(ct)), response.Content.Headers.ContentLength, contentType, hash, canSeek: false, modified);
    }

    // Writes and host-local write handles live in DriveStorage.Writes.cs.

    // ----- helpers -----

    // A folder to list: My Drive, a folder, or the target of a folder shortcut (one hop only).
    private async Task<string> ListableFolder(ItemId folder, CancellationToken ct)
    {
        if (folder.Value is null or Root) { return Root; }
        var file = await Metadata(folder, ct);
        var type = file.GetProperty("mimeType").GetString();
        if (type == FolderType) { return folder.Value; }
        if (type == ShortcutType && file.TryGetProperty("shortcutDetails", out var details)
            && details.TryGetProperty("targetMimeType", out var targetType) && targetType.GetString() == FolderType
            && details.TryGetProperty("targetId", out var target) && target.GetString() is { Length: > 0 } targetId)
        { return targetId; }
        throw new Unsupported(folder);
    }

    private async Task<Page<Item>> Page(string operation, string q, string orderBy, int pageSize, string? cursor, ItemId? item, CancellationToken ct)
    {
        var path = $"files?q={Uri.EscapeDataString(q)}&fields={Uri.EscapeDataString($"nextPageToken,files({ItemFields})")}"
            + $"&orderBy={Uri.EscapeDataString(orderBy)}&pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
            + (cursor is { Length: > 0 } ? $"&pageToken={Uri.EscapeDataString(cursor)}" : "");
        using var json = await StorageJson(operation, path, item, ct);
        var root = json.RootElement;
        var items = root.TryGetProperty("files", out var files) ? files.EnumerateArray().Select(ToItem).ToList() : [];
        var next = root.TryGetProperty("nextPageToken", out var token) && token.GetString() is { Length: > 0 } value ? value : null;
        return new Page<Item>(items, next);
    }

    private static string OrderBy(SortKey sort, bool descending)
    {
        var keys = sort switch
        {
            SortKey.Name => ["name_natural"],
            SortKey.Modified => ["modifiedTime"],
            SortKey.Size => ["quotaBytesUsed"],
            _ => new[] { "folder", "name_natural" },
        };
        return string.Join(',', keys.Select(key => descending ? key + " desc" : key));
    }

    private async Task<JsonElement> Metadata(ItemId id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) { throw new NotFound(id); }
        using var json = await StorageJson(GoogleDriveOperations.Metadata, $"files/{Uri.EscapeDataString(id.Value)}?fields={Uri.EscapeDataString(ItemFields)}", id, ct);
        if (json.RootElement.TryGetProperty("trashed", out var trashed) && trashed.ValueKind == JsonValueKind.True) { throw new NotFound(id); }
        return json.RootElement.Clone();
    }

    private async Task<(byte[] Bytes, string MediaType, long? Total)> ReadBytes(ItemId id, ByteRange? range, CancellationToken ct)
    {
        var file = await Metadata(id, ct);
        var type = file.GetProperty("mimeType").GetString() ?? "";
        if (type == FolderType || type == ShortcutType) { throw new Unsupported(id); }
        if (type == DocumentType)
        {
            var text = await Storage(GoogleDriveOperations.Export, $"files/{Uri.EscapeDataString(id.Value!)}/export?mimeType=text%2Fplain", id, ct,
                response => response.Content.ReadAsByteArrayAsync(ct));
            return (Slice(text, range), "text/plain", text.Length);
        }
        if (type.StartsWith(GooglePrefix, StringComparison.Ordinal)) { throw new Unsupported(id); }
        var size = ToItem(file).Size;
        var (bytes, mediaType, partial) = await Storage(GoogleDriveOperations.Media, $"files/{Uri.EscapeDataString(id.Value!)}?alt=media", id, ct,
            async response => (await response.Content.ReadAsByteArrayAsync(ct), response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                response.StatusCode == HttpStatusCode.PartialContent), range);
        // A server that ignores Range sends the whole file (200): cut it here.
        return (range is not null && !partial ? Slice(bytes, range) : bytes, mediaType, size);
    }

    private static byte[] Slice(byte[] bytes, ByteRange? range)
    {
        if (range is not { } r) { return bytes; }
        var offset = (int)Math.Min(r.Offset, bytes.Length);
        var length = r.Length is { } l ? (int)Math.Min(l, bytes.Length - offset) : bytes.Length - offset;
        return bytes.AsSpan(offset, length).ToArray();
    }

    private Item ToItem(JsonElement file)
    {
        var type = file.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null;
        var kind = type == FolderType ? ItemKind.Folder : type == ShortcutType ? ItemKind.Link : ItemKind.File;
        long? size = file.TryGetProperty("size", out var sizeElement) && long.TryParse(sizeElement.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        DateTimeOffset? modified = file.TryGetProperty("modifiedTime", out var time)
            && DateTimeOffset.TryParse(time.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when) ? when : null;
        ItemId? target = kind == ItemKind.Link && file.TryGetProperty("shortcutDetails", out var details) && details.TryGetProperty("targetId", out var targetId)
            ? new ItemId(targetId.GetString()!) : null;
        return new Item(new ItemId(file.GetProperty("id").GetString()!), file.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "", kind,
            kind == ItemKind.File ? size : null, modified, Flags(file), file.TryGetProperty("md5Checksum", out var md5) ? md5.GetString() : null, target);
    }

    // Item permissions are intersected with the actual grant, never inferred from deployment options.
    private ItemFlags Flags(JsonElement file)
    {
        var flags = ItemFlags.Read;
        var folder = file.TryGetProperty("mimeType", out var type) && type.GetString() == FolderType;
        if (file.TryGetProperty("capabilities", out var capabilities))
        {
            if (capabilities.TryGetProperty("canTrash", out var trash) && trash.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Trash; }
            if (capabilities.TryGetProperty("canDelete", out var delete) && delete.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Delete; }
            if (!folder && capabilities.TryGetProperty("canModifyContent", out var edit) && edit.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Write; }
            if (capabilities.TryGetProperty("canRename", out var rename) && rename.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Rename; }
            if (capabilities.TryGetProperty("canMoveItemWithinDrive", out var move) && move.ValueKind == JsonValueKind.True
                || capabilities.TryGetProperty("canMoveItemOutOfDrive", out var moveOut) && moveOut.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Move; }
            if (capabilities.TryGetProperty("canCopy", out var copy) && copy.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Copy; }
            if (folder && capabilities.TryGetProperty("canListChildren", out var list) && list.ValueKind == JsonValueKind.True) { flags |= ItemFlags.Copy; }
            if (capabilities.TryGetProperty("canAddChildren", out var add) && add.ValueKind == JsonValueKind.True) { flags |= ItemFlags.CreateFolder | ItemFlags.Write; }
        }
        return flags & Offered;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private async Task<JsonDocument> StorageJson(string operation, string path, ItemId? item, CancellationToken ct) =>
        await Storage(operation, path, item, ct, async response => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct));

    // One storage@1 request: failures become typed storage errors (DriveErrors), and a refused token marks the connection expired.
    private async Task<T> Storage<T>(string operation, string path, ItemId? item, CancellationToken ct, Func<HttpResponseMessage, Task<T>> read, ByteRange? range = null)
    {
        using var response = await OpenStorage(operation, path, item, range, ct);
        return await read(response);
    }

    private async Task<HttpResponseMessage> OpenStorage(string operation, string path, ItemId? item, ByteRange? range, CancellationToken ct)
    {
        using var trace = EnterTrace();
        return await UseCredentialAsync(async (credential, token) =>
        {
            var response = await RequestAsync(new Uri(Options.Endpoint, path), operation, credential.Value, token, Range(range));
            if (response.IsSuccessStatusCode) { return response; }
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized) { await MarkExpired("The Google Drive access token was refused. Reconnect Google Drive."); }
                throw DriveErrors.Map(response.StatusCode, DriveErrors.ReasonOf(await response.Content.ReadAsStringAsync(token)), item);
            }
        }, ct);
    }

    private static RangeHeaderValue? Range(ByteRange? range)
        => range is { } r ? new RangeHeaderValue(r.Offset, r.Length is { } length ? r.Offset + Math.Max(0, length - 1) : null) : null;

    // The response body as a stream that disposes its response.
    private sealed class ResponseStream(HttpResponseMessage response, Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); response.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
