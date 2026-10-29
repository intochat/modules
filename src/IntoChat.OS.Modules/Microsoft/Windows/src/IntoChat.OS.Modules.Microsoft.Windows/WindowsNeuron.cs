using System.Text;
using System.Text.Json;
using IntoChat.OS.Framework.Machine;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Integrations;
using IntoChat.OS.Runtime.Storage.Confinement;
using StorageV1 = DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.Microsoft.Windows;

[GenerateSerializer, Alias("windows.connection-state")]
internal sealed class WindowsConnectionState : ConnectionState
{
    [Id(0)] public string[] Roots { get; set; } = [];
}

// The connection to one Windows PC. Its key names the machine; only a silo whose machine id matches serves it,
// and connecting proves the device value is this very machine. As storage, every location is confined
// to the allowed roots, read fresh on every call.
[DigitalBrain.Neuron("windows", State = "windows")]
internal sealed class WindowsNeuron(
    IMachineIdentity identity, WindowsStorageCoordinator coordinator)
    : ConnectionNeuron<WindowsConnectionState>, IWindows, ILegacyStorage, StorageV1.IStorage, StorageV1.IStorageUpload
{
    private StorageV1.StorageUploadSessions? _uploads;

    public Task<WindowsFacts> Facts() => Task.FromResult(new WindowsFacts(
        identity.Id, Environment.MachineName, Environment.OSVersion.VersionString, Environment.ProcessorCount,
        GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));

    public Task<string[]> Roots() => Task.FromResult(Snapshot.Roots);

    public async Task AllowRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = PathConfinement.Normalize(path);
        if (!Directory.Exists(full)) { throw new DirectoryNotFoundException($"'{full}' is not a folder on this machine."); }
        if (Snapshot.Roots.Contains(full, StringComparer.OrdinalIgnoreCase)) { return; }
        Snapshot.Roots = [.. Snapshot.Roots, full];
        await SaveQuietly(Snapshot);
    }

    public async Task RevokeRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = PathConfinement.Normalize(path);
        Snapshot.Roots = Snapshot.Roots.Where(root => !string.Equals(root, full, StringComparison.OrdinalIgnoreCase)).ToArray();
        await SaveQuietly(Snapshot);
    }

    public Task<StorageEntry[]> Roots(CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshot.Roots.Select(root => new StorageEntry(root, Name(root), true)).ToArray());

    public async Task<StorageEntry[]> List(string location, CancellationToken cancellationToken = default)
    {
        var folder = PathConfinement.Resolve(Snapshot.Roots, location);
        var storage = Storage;
        var items = new List<StorageEntry>();
        string? cursor = null;
        do
        {
            var page = await storage.List(WindowsStorage.IdOf(folder), new StorageV1.ListQuery(cursor, StorageV1.ListQuery.MaxPageSize), cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Items.Select(item => new StorageEntry(Path.Combine(folder, item.Name), item.Name, item.Kind == StorageV1.ItemKind.Folder)));
            cursor = page.NextCursor;
        }
        while (cursor is not null);
        return [.. items];
    }

    public async Task<string> Read(string location, CancellationToken cancellationToken = default)
    {
        var full = PathConfinement.Resolve(Snapshot.Roots, location);
        var content = await Storage.Read(WindowsStorage.IdOf(full), null, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(content.Bytes.Span);
    }

    public Task<StorageEntry[]> Ancestors(string location, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(location)) { return Task.FromResult<StorageEntry[]>([]); }
        var full = PathConfinement.Resolve(Snapshot.Roots, location);
        var root = PathConfinement.Containing(Snapshot.Roots, full);
        var chain = new List<StorageEntry> { new(root, Name(root), true) };
        var path = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            path = Path.Combine(path, part);
            chain.Add(new(path, part, Directory.Exists(path)));
        }
        return Task.FromResult(chain.ToArray());
    }

    // storage@1 (05-08): the same allowed roots, read fresh on every call, through WindowsStorage. Explicit members: the
    // 0.6 ILegacyStorage members share their names.
    private WindowsStorage Storage => new(Snapshot.Roots, operations: Context.Operations, currentRoots: () => Snapshot.Roots, coordinator: coordinator);
    private StorageV1.StorageUploadSessions Uploads => _uploads ??= new(() => Storage, UploadCaller, Context.Time);

    private static string UploadCaller()
    {
        var caller = CallerContextStamper.Require();
        return JsonSerializer.Serialize(new[] { caller.AccountId, caller.BrainId, caller.PrincipalId, caller.Kind.ToString(), caller.AppId, caller.ConversationId });
    }

    Task<StorageV1.StorageDescriptor> StorageV1.IStorage.Describe(CancellationToken ct) => Storage.Describe(ct);
    Task<IReadOnlyList<StorageV1.Item>> StorageV1.IStorage.Roots(CancellationToken ct) => Storage.Roots(ct);
    Task<StorageV1.Page<StorageV1.Item>> StorageV1.IStorage.List(StorageV1.ItemId folder, StorageV1.ListQuery query, CancellationToken ct) => Storage.List(folder, query, ct);
    Task<StorageV1.Item> StorageV1.IStorage.Stat(StorageV1.ItemId id, CancellationToken ct) => Storage.Stat(id, ct);
    Task<StorageV1.Content> StorageV1.IStorage.Read(StorageV1.ItemId id, StorageV1.ByteRange? range, CancellationToken ct) => Storage.Read(id, range, ct);
    async Task<DigitalBrain.Operations.OperationId> StorageV1.IStorage.Write(StorageV1.WritePlan plan, CancellationToken ct)
    {
        await AuthorizeWrite(plan, ct);
        return await Storage.Write(plan, ct);
    }
    Task<StorageV1.Page<StorageV1.Item>> StorageV1.IStorage.Search(StorageV1.SearchQuery query, CancellationToken ct) => Storage.Search(query, ct);
    Task<IReadOnlyList<StorageV1.Item>> StorageV1.IStorage.Ancestors(StorageV1.ItemId id, CancellationToken ct) => Storage.Ancestors(id, ct);
    Task<StorageV1.StorageReadStream> StorageV1.IStorage.OpenRead(StorageV1.ItemId item, StorageV1.ByteRange? range, CancellationToken ct) => Storage.OpenRead(item, range, ct);
    async Task<StorageV1.StorageWriteStream> StorageV1.IStorage.OpenWrite(StorageV1.WritePlan plan, CancellationToken ct)
    {
        await AuthorizeWrite(plan, ct);
        return await Storage.OpenWrite(plan, ct);
    }

    async Task<string> StorageV1.IStorageUpload.BeginUpload(StorageV1.WritePlan plan, CancellationToken ct)
    {
        await AuthorizeWrite(plan, ct);
        return await Uploads.BeginUpload(plan, ct);
    }
    Task StorageV1.IStorageUpload.AppendUpload(string upload, long offset, byte[] bytes, CancellationToken ct) => Uploads.AppendUpload(upload, offset, bytes, ct);
    Task<StorageV1.ItemId> StorageV1.IStorageUpload.CommitUpload(string upload, long length, CancellationToken ct) => Uploads.CommitUpload(upload, length, ct);
    Task StorageV1.IStorageUpload.AbortUpload(string upload, CancellationToken ct) => Uploads.AbortUpload(upload, ct);

    private async Task AuthorizeWrite(StorageV1.WritePlan plan, CancellationToken ct)
    {
        if (await StorageV1.StorageConfirmationGate.Check(Context, Storage, plan, ct) is not null)
        { throw new StorageV1.Denied(plan.Source ?? plan.TargetFolder); }
    }

    protected override async Task OnDeactivate(DigitalBrain.DeactivationCause cause, CancellationToken cancellationToken)
    {
        if (_uploads is not null) { await _uploads.DisposeAsync(); }
    }

    private static string Name(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;

    protected override Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct) =>
        Task.FromResult(OperatingSystem.IsWindows() && credential.Value == identity.Id
            ? ConnectionProbeResult.Connected()
            : ConnectionProbeResult.Failing("This device value is not the machine this silo runs on."));

    internal void RequireThisMachine()
    {
        if (Key.Connection != identity.Id)
        { throw new ServiceUnavailableException($"Windows machine '{Key.Connection}' is not this silo's machine."); }
    }

    // An activation for another machine fails before any call (Facts, Connect, Probe) reaches the neuron.
    protected override Task Activated(CancellationToken cancellationToken)
    {
        RequireThisMachine();
        return Task.CompletedTask;
    }
}
