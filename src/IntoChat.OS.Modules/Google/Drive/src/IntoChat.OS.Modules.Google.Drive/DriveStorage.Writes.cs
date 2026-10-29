using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Operations;
using DigitalBrain.Spi;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Google.Drive;

internal sealed partial class DriveNeuron : IStorageUpload
{
    private StorageUploadSessions? _uploads;
    private StorageUploadSessions Uploads => _uploads ??= new(OpenWriteCore, UploadCaller, Context.Time);
    private bool CanWrite => DriveScopes.GrantsWrite(Snapshot.Scopes);

    public async Task<string> BeginUpload(WritePlan plan, CancellationToken ct = default)
    {
        await ConfirmWrite(plan, ct);
        return await Uploads.BeginUpload(plan, ct);
    }
    public Task AppendUpload(string upload, long offset, byte[] bytes, CancellationToken ct = default) => Uploads.AppendUpload(upload, offset, bytes, ct);
    public Task<ItemId> CommitUpload(string upload, long length, CancellationToken ct = default) => Uploads.CommitUpload(upload, length, ct);
    public Task AbortUpload(string upload, CancellationToken ct = default) => Uploads.AbortUpload(upload, ct);

    private string UploadCaller()
    {
        using var trace = EnterTrace();
        var caller = CallerContextStamper.Require();
        return JsonSerializer.Serialize(new[] { caller.AccountId, caller.BrainId, caller.PrincipalId,
            caller.Kind.ToString(), caller.AppId, caller.ConversationId });
    }

    protected override async Task OnDeactivate(DigitalBrain.DeactivationCause cause, CancellationToken cancellationToken)
    {
        if (_uploads is { } uploads) { await uploads.DisposeAsync(); }
        await base.OnDeactivate(cause, cancellationToken);
    }

    async Task<OperationId> IStorage.Write(WritePlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await ConfirmWrite(plan, ct);
        var operations = ServiceProvider.GetService<IOperations>() ?? Context.Operations;
        var operation = await operations.Start(new DriveWriteOperation(plan.Kind, plan.DryRun), ct);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, operation.Cancellation);
        try
        {
            var prepared = await Prepare(plan, linked.Token);
            var copy = plan.Kind == WriteKind.Copy ? await PlanCopy(prepared.Source!, new HashSet<ItemId>(), 0, linked.Token) : null;
            if (plan.DryRun)
            {
                await operation.Complete(new OperationResult.Planned([$"{plan.Kind}: {prepared.Name}"]));
                return operation.Id;
            }
            await operation.Report(0);
            await Apply(plan, prepared, copy, linked.Token);
            await operation.Report(1);
            await operation.Complete(new OperationResult.Succeeded());
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        { await operation.Complete(new OperationResult.Cancelled()); }
        catch (StorageException error)
        { await operation.Complete(new OperationResult.Failed(error.Code, error.Message)); }
        catch (HttpRequestException)
        { await operation.Complete(new OperationResult.Failed("unavailable", "Google Drive could not complete the operation.")); }
        catch (IOException)
        { await operation.Complete(new OperationResult.Failed("unavailable", "Google Drive could not complete the operation.")); }
        catch (JsonException)
        { await operation.Complete(new OperationResult.Failed("unavailable", "Google Drive returned an unreadable response.")); }
        return operation.Id;
    }

    async Task<StorageWriteStream> IStorage.OpenWrite(WritePlan plan, CancellationToken ct)
    {
        await ConfirmWrite(plan, ct);
        return await OpenWriteCore(plan, ct);
    }

    private async Task<StorageWriteStream> OpenWriteCore(WritePlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireWrite();
        if (plan.Kind != WriteKind.WriteFile || plan.DryRun || plan.Inline is not null) { throw new Unsupported(plan.TargetFolder); }
        _ = await Prepare(plan, ct); // Conflict is refused before any bytes are accepted.
        var path = Path.Combine(Path.GetTempPath(), "intochat-drive-" + Guid.NewGuid().ToString("N"));
        var content = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        return new BufferedWrite(content, async token =>
        {
            RequireWrite(); // A reconnected read-only grant cannot finish an earlier upload.
            var prepared = await Prepare(plan, token);
            content.Position = 0;
            return await Upload(prepared, content, token);
        });
    }

    private void RequireWrite()
    {
        using var trace = EnterTrace();
        if (!CanWrite) { throw new Unsupported(null); }
    }

    private async Task ConfirmWrite(WritePlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireWrite();
        if (await StorageConfirmationGate.Check(Context, this, plan, ct) is not null)
        { throw new Denied(plan.Source ?? plan.TargetFolder); }
    }

    private async Task<PreparedWrite> Prepare(WritePlan plan, CancellationToken ct)
    {
        if (!Enum.IsDefined(plan.Kind) || !Enum.IsDefined(plan.Conflict)) { throw new Unsupported(null); }
        var source = plan.Source is { } id ? ToItem(await Metadata(id, ct)) : null;
        if (plan.Kind is WriteKind.Move or WriteKind.Copy or WriteKind.Rename or WriteKind.Delete && source is null)
        { throw new NotFound(plan.Source); }
        if (source is not null && (plan.Source!.Value.Value == Root || source.Id.Value == (await Metadata(new(Root), ct)).GetProperty("id").GetString()))
        { throw new Denied(source.Id); }
        var flag = plan.Kind switch
        {
            WriteKind.Rename => ItemFlags.Rename,
            WriteKind.Copy => ItemFlags.Copy,
            WriteKind.Move => ItemFlags.Move,
            WriteKind.Delete => plan.Permanent ? ItemFlags.Delete : ItemFlags.Trash,
            _ => ItemFlags.None,
        };
        if (source is not null && !source.Flags.HasFlag(flag)) { throw new Denied(source.Id); }
        ItemId? folder = null;
        if (plan.Kind == WriteKind.Rename)
        {
            var chain = await ((IStorage)this).Ancestors(source!.Id, ct);
            folder = chain.Count > 0 ? chain[^1].Id : throw new Denied(source.Id);
        }
        else if (plan.Kind != WriteKind.Delete)
        { folder = new ItemId(await ListableFolder(plan.TargetFolder ?? throw new NotFound(null), ct)); }
        if (folder is { } into)
        {
            var parent = await ((IStorage)this).Stat(into, ct);
            if (!parent.Flags.HasFlag(plan.Kind == WriteKind.CreateFolder ? ItemFlags.CreateFolder : ItemFlags.Write)) { throw new Denied(into); }
            if (source is { Kind: ItemKind.Folder } && (source.Id == into || (await ((IStorage)this).Ancestors(into, ct)).Any(item => item.Id == source.Id)))
            { throw new Denied(into); }
        }
        var name = plan.Kind is WriteKind.CreateFolder or WriteKind.WriteFile or WriteKind.Rename ? plan.Name : source?.Name;
        if (string.IsNullOrWhiteSpace(name)) { throw new Unsupported(plan.Source ?? plan.TargetFolder); }
        if (folder is null) { return new(source, null, name, null); }
        var items = await Children(folder.Value, ct);
        var collisions = items.Where(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && !(plan.Kind is WriteKind.Rename or WriteKind.Move && item.Id == source?.Id)).ToArray();
        if (collisions.Length == 0) { return new(source, folder, name, null); }
        if (plan.Conflict == ConflictPolicy.Fail) { throw new Conflict(collisions[0].Id); }
        if (plan.Conflict == ConflictPolicy.Rename) { return new(source, folder, StorageWriteRunner.FreeName(name, items.Select(item => item.Name)), null); }
        if (collisions.Length != 1 || collisions[0].Kind != ItemKind.File || collisions[0].Id == source?.Id || plan.Kind == WriteKind.CreateFolder)
        { throw new Conflict(collisions[0].Id); }
        if (!collisions[0].Flags.HasFlag(plan.Kind == WriteKind.WriteFile ? ItemFlags.Write : ItemFlags.Trash))
        { throw new Denied(collisions[0].Id); }
        return new(source, folder, name, collisions[0]);
    }

    private async Task<List<Item>> Children(ItemId folder, CancellationToken ct)
    {
        var items = new List<Item>();
        string? cursor = null;
        do
        {
            var page = await ((IStorage)this).List(folder, new(Cursor: cursor, PageSize: ListQuery.MaxPageSize), ct);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }

    private async Task Apply(WritePlan plan, PreparedWrite prepared, CopyNode? copy, CancellationToken ct)
    {
        var sourcePath = prepared.Source is { } source ? "files/" + Uri.EscapeDataString(source.Id.Value) : "files";
        switch (plan.Kind)
        {
            case WriteKind.WriteFile:
                if (plan.Inline is not { } bytes) { throw new Unsupported(plan.TargetFolder); }
                using (var content = new MemoryStream(bytes.ToArray(), writable: false)) { _ = await Upload(prepared, content, ct); }
                return;
            case WriteKind.CreateFolder:
                _ = await Mutate(HttpMethod.Post, "files", GoogleDriveOperations.Create,
                    new DriveMetadata(prepared.Name, FolderType, [prepared.Folder!.Value.Value]), null, ct);
                return;
            case WriteKind.Delete:
                _ = await Mutate(plan.Permanent ? HttpMethod.Delete : HttpMethod.Patch, sourcePath, GoogleDriveOperations.Delete,
                    plan.Permanent ? null : new DriveMetadata(Trashed: true), plan.Source, ct);
                return;
            case WriteKind.Copy:
                await Copy(copy!, prepared.Folder!.Value, prepared.Name, ct);
                break;
            case WriteKind.Move:
                var old = (await Metadata(prepared.Source!.Id, ct)).GetProperty("parents").EnumerateArray().Select(item => item.GetString()!);
                _ = await Mutate(HttpMethod.Patch, sourcePath + "?addParents=" + Uri.EscapeDataString(prepared.Folder!.Value.Value)
                    + "&removeParents=" + Uri.EscapeDataString(string.Join(',', old)), GoogleDriveOperations.Update,
                    new DriveMetadata(prepared.Name), plan.Source, ct);
                break;
            case WriteKind.Rename:
                _ = await Mutate(HttpMethod.Patch, sourcePath, GoogleDriveOperations.Update, new DriveMetadata(prepared.Name), plan.Source, ct);
                break;
        }
        // Replacement was explicitly requested. Preserve the old target until the new operation succeeds.
        if (prepared.Existing is { } replaced)
        { _ = await Mutate(HttpMethod.Patch, "files/" + Uri.EscapeDataString(replaced.Id.Value), GoogleDriveOperations.Delete, new DriveMetadata(Trashed: true), replaced.Id, ct); }
    }

    private async Task<CopyNode> PlanCopy(Item item, HashSet<ItemId> seen, int depth, CancellationToken ct)
    {
        if (!item.Flags.HasFlag(ItemFlags.Copy)) { throw new Denied(item.Id); }
        if (!seen.Add(item.Id) || depth >= MaxDepth) { throw new Unsupported(item.Id); }
        var children = new List<CopyNode>();
        if (item.Kind == ItemKind.Folder)
        {
            foreach (var child in await Children(item.Id, ct)) { children.Add(await PlanCopy(child, seen, depth + 1, ct)); }
        }
        return new(item, children);
    }

    private async Task Copy(CopyNode node, ItemId folder, string name, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (node.Item.Kind != ItemKind.Folder)
        {
            _ = await Mutate(HttpMethod.Post, "files/" + Uri.EscapeDataString(node.Item.Id.Value) + "/copy", GoogleDriveOperations.Copy,
                new DriveMetadata(name, Parents: [folder.Value]), node.Item.Id, ct);
            return;
        }
        var created = await Mutate(HttpMethod.Post, "files", GoogleDriveOperations.Create, new DriveMetadata(name, FolderType, [folder.Value]), folder, ct);
        var target = ResponseId(created!.Value, folder);
        foreach (var child in node.Children) { await Copy(child, target, child.Item.Name, ct); }
    }

    private async Task<ItemId> Upload(PreparedWrite prepared, Stream content, CancellationToken ct)
    {
        try { return await UploadCore(prepared, content, ct); }
        catch (HttpRequestException error) { throw new Unavailable(prepared.Folder, error); }
        catch (IOException error) { throw new Unavailable(prepared.Folder, error); }
        catch (JsonException error) { throw new Unavailable(prepared.Folder, error); }
    }

    private async Task<ItemId> UploadCore(PreparedWrite prepared, Stream content, CancellationToken ct)
    {
        var update = prepared.Existing;
        var uploadBase = new UriBuilder(Options.Endpoint) { Path = "/upload" + Options.Endpoint.AbsolutePath }.Uri;
        var uri = new Uri(uploadBase, update is null ? "files?uploadType=resumable&fields=id" : "files/" + Uri.EscapeDataString(update.Id.Value) + "?uploadType=resumable&fields=id");
        using var trace = EnterTrace();
        return await UseCredentialAsync(async (credential, token) =>
        {
            using var initial = await RequestAsync(uri, update is null ? GoogleDriveOperations.Create : GoogleDriveOperations.Update, credential.Value, token,
                method: update is null ? HttpMethod.Post : HttpMethod.Patch,
                content: JsonContent.Create(new DriveMetadata(prepared.Name, Parents: update is null ? [prepared.Folder!.Value.Value] : null), options: WriteJson));
            await CheckResponse(initial, update?.Id, token);
            var session = initial.Headers.Location;
            if (session is null || !session.IsAbsoluteUri || session.Scheme != uri.Scheme || session.Authority != uri.Authority || session.UserInfo.Length != 0)
            { throw new Unavailable(prepared.Folder); }
            using var body = new StreamContent(content);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            body.Headers.ContentLength = content.Length;
            using var response = await RequestAsync(session, update is null ? GoogleDriveOperations.Create : GoogleDriveOperations.Update, credential.Value, token,
                method: HttpMethod.Put, content: body);
            await CheckResponse(response, update?.Id, token);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
            return ResponseId(json.RootElement, prepared.Folder);
        }, ct);
    }

    private static readonly JsonSerializerOptions WriteJson = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private async Task<JsonElement?> Mutate(HttpMethod method, string path, string operation, DriveMetadata? data, ItemId? item, CancellationToken ct)
    {
        using var trace = EnterTrace();
        try
        {
            return await UseCredentialAsync(async (credential, token) =>
            {
                using var response = await RequestAsync(new Uri(Options.Endpoint, path), operation, credential.Value, token,
                    method: method, content: data is null ? null : JsonContent.Create(data, options: WriteJson));
                await CheckResponse(response, item, token);
                if (response.StatusCode == HttpStatusCode.NoContent) { return (JsonElement?)null; }
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
                return json.RootElement.Clone();
            }, ct);
        }
        catch (HttpRequestException error) { throw new Unavailable(item, error); }
        catch (JsonException error) { throw new Unavailable(item, error); }
    }

    private static ItemId ResponseId(JsonElement response, ItemId? item)
        => response.ValueKind == JsonValueKind.Object && response.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } value ? new(value) : throw new Unavailable(item);

    private async Task CheckResponse(HttpResponseMessage response, ItemId? item, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) { return; }
        if (response.StatusCode == HttpStatusCode.Unauthorized) { await MarkExpired("The Google Drive access token was refused. Reconnect Google Drive."); }
        throw DriveErrors.Map(response.StatusCode, DriveErrors.ReasonOf(await response.Content.ReadAsStringAsync(ct)), item);
    }

    private sealed record DriveWriteOperation(WriteKind Kind, bool DryRun) : OperationPlan(DryRun);
    private sealed record PreparedWrite(Item? Source, ItemId? Folder, string Name, Item? Existing);
    private sealed record CopyNode(Item Item, IReadOnlyList<CopyNode> Children);
    private sealed record DriveMetadata(string? Name = null, string? MimeType = null, string[]? Parents = null, bool? Trashed = null);
    private sealed class BufferedWrite(Stream content, Func<CancellationToken, Task<ItemId>> commit) : StorageWriteStream(content)
    {
        protected override Task<ItemId> CommitCore(CancellationToken ct) => commit(ct);
        protected override Task AbortCore(CancellationToken ct) => Task.CompletedTask;
    }
}
