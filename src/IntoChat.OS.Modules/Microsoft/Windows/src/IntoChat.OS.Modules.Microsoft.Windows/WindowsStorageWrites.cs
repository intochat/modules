using System.Security.Cryptography;
using System.Text.Json;
using DigitalBrain.Capabilities.Storage;
using DigitalBrain.Operations;
using DigitalBrain.Spi;
using IntoChat.OS.Framework.Diagnostics;
using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.Microsoft.Windows;

internal sealed partial class WindowsStorage
{
    internal const string TrashFolderName = ".intochat-trash";
    private const string Marker = "intochat.windows.storage.trash.v1";
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public async Task<OperationId> Write(WritePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ct.ThrowIfCancellationRequested();
        var host = operations ?? throw new InvalidOperationException("Windows storage writes need the host's operations.");
        // A provider primitive is manually driven. Starting WritePlan here would recursively invoke the storage runner.
        var operation = await host.Start(new WindowsStorageOperation(plan with { Inline = null }, plan.Inline?.Length,
            plan.Inline is { } bytes ? Convert.ToHexStringLower(SHA256.HashData(bytes.Span)) : null), ct).ConfigureAwait(false);
        _ = Task.Run(() => CompleteWrite(plan, operation, ct), CancellationToken.None);
        return operation.Id;
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
                await Apply(plan, prepared, ct).ConfigureAwait(false);
                await operation.Report(1).ConfigureAwait(false);
                result = new OperationResult.Succeeded();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { result = new OperationResult.Cancelled(); }
        catch (StorageException error) { result = new OperationResult.Failed(error.Code, error.Message); }
        catch (UnauthorizedAccessException) { result = new OperationResult.Failed("denied", "Storage access was denied."); }
        catch (IOException error) { result = new OperationResult.Failed(IoCode(error), "The storage change could not be completed."); }
        catch (ArgumentException) { result = new OperationResult.Failed("invalid-plan", "The storage plan is invalid."); }
        await operation.Complete(result).ConfigureAwait(false);
    }

    private (string? Source, string? Destination) Prepare(WritePlan plan)
    {
        if (!Enum.IsDefined(plan.Kind) || !Enum.IsDefined(plan.Conflict)) { throw new Unsupported(plan.Source); }
        var source = plan.Source is { } item ? Resolve(item) : null;
        if (source is not null)
        {
            if (!Exists(source)) { throw new NotFound(plan.Source); }
            if (IsRoot(source)) { throw new Denied(plan.Source); }
        }
        string? destination = null;
        switch (plan.Kind)
        {
            case WriteKind.CreateFolder or WriteKind.WriteFile:
                destination = Path.Combine(Folder(plan.TargetFolder), SafeName(plan.Name));
                break;
            case WriteKind.Copy or WriteKind.Move:
                if (source is null) { throw new Unsupported(null); }
                destination = Path.Combine(Folder(plan.TargetFolder), Path.GetFileName(source));
                if (Directory.Exists(source) && (IsInside(source, destination) || IsInside(destination, source))) { throw new Denied(plan.Source); }
                break;
            case WriteKind.Rename:
                if (source is null) { throw new Unsupported(null); }
                destination = Path.Combine(Path.GetDirectoryName(source)!, SafeName(plan.Name));
                break;
            case WriteKind.Delete:
                if (source is null) { throw new Unsupported(null); }
                break;
        }
        if (destination is not null) { _ = Resolve(IdOf(destination)); }
        return (source, destination);
    }

    private async Task Apply(WritePlan plan, (string? Source, string? Destination) prepared, CancellationToken ct)
    {
        if (plan.Kind == WriteKind.WriteFile)
        {
            if (plan.Inline is not { } content) { throw new Unsupported(plan.TargetFolder); }
            await using var writer = await OpenWrite(plan, ct).ConfigureAwait(false);
            await writer.Content.WriteAsync(content, ct).ConfigureAwait(false);
            _ = await writer.Commit(ct).ConfigureAwait(false);
            return;
        }
        var anchor = prepared.Destination ?? prepared.Source!;
        var root = PathConfinement.Containing(AllowedRoots, anchor);
        var gate = _coordinator.Gates.GetOrAdd(root, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (source, destination) = Prepare(plan);
            ct.ThrowIfCancellationRequested();
            if (plan.Kind == WriteKind.Delete)
            {
                ValidateTree(source!, ct);
                if (plan.Permanent) { DeletePath(source!); }
                else { Trash(source!); }
                return;
            }
            if (source is not null && plan.Kind is WriteKind.Move or WriteKind.Rename && PathComparer.Equals(source, destination))
            {
                if (string.Equals(source, destination, StringComparison.Ordinal)) { return; }
                ValidateTree(source, ct);
                var intermediate = StagePath(Path.GetDirectoryName(source)!);
                MovePath(source, intermediate);
                try { MovePath(intermediate, destination!); }
                catch { MovePath(intermediate, source); throw; }
                return;
            }
            destination = Destination(destination!, plan.Conflict, plan.Kind is WriteKind.Move or WriteKind.Rename ? source : null);
            if (plan.Kind == WriteKind.CreateFolder && !Exists(destination))
            {
                _ = Resolve(IdOf(destination));
                Directory.CreateDirectory(destination);
                return;
            }
            var stage = StagePath(Path.GetDirectoryName(destination)!);
            try
            {
                if (plan.Kind == WriteKind.CreateFolder) { Directory.CreateDirectory(stage); }
                else
                {
                    ValidateTree(source!, ct);
                    await CopyTree(source!, stage, ct).ConfigureAwait(false);
                }
                ct.ThrowIfCancellationRequested();
                _ = Prepare(plan);
                destination = Destination(destination, plan.Conflict, plan.Kind is WriteKind.Move or WriteKind.Rename ? source : null);
                Publish(stage, destination, plan.Conflict);
                if (plan.Kind is WriteKind.Move or WriteKind.Rename)
                {
                    // A verified destination is kept if removing the source fails: a duplicate is safer than losing either.
                    try { Trash(Resolve(IdOf(source!))); }
                    catch (StorageException) { throw new SourceRemovalFailed(); }
                    catch (IOException) { throw new SourceRemovalFailed(); }
                    catch (UnauthorizedAccessException) { throw new SourceRemovalFailed(); }
                }
            }
            finally { Cleanup(stage); }
        }
        finally { gate.Release(); }
    }

    public Task<StorageWriteStream> OpenWrite(WritePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ct.ThrowIfCancellationRequested();
        if (plan.Kind != WriteKind.WriteFile || plan.DryRun) { throw new Unsupported(plan.TargetFolder); }
        return Task.FromResult(Guard<StorageWriteStream>(plan.TargetFolder, () =>
        {
            var destination = Prepare(plan).Destination!;
            _ = Destination(destination, plan.Conflict, null);
            var stage = StagePath(Path.GetDirectoryName(destination)!);
            return new WriteHandle(this, plan, stage, new FileStream(stage, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous));
        }));
    }

    private async Task<ItemId> Commit(WritePlan plan, string stage, CancellationToken ct)
    {
        var destination = Prepare(plan).Destination!;
        var root = PathConfinement.Containing(AllowedRoots, destination);
        var gate = _coordinator.Gates.GetOrAdd(root, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            destination = Destination(Prepare(plan).Destination!, plan.Conflict, null);
            Guard(plan.TargetFolder, () => { Publish(stage, destination, plan.Conflict); return true; });
            return IdOf(destination);
        }
        finally { gate.Release(); }
    }

    private sealed class WriteHandle(WindowsStorage owner, WritePlan plan, string stage, FileStream stream) : StorageWriteStream(stream)
    {
        protected override async Task<ItemId> CommitCore(CancellationToken ct)
        {
            try
            {
                await stream.FlushAsync(ct).ConfigureAwait(false);
                await stream.DisposeAsync().ConfigureAwait(false);
                return await owner.Commit(plan, stage, ct).ConfigureAwait(false);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                owner.Cleanup(stage);
            }
        }

        protected override async Task AbortCore(CancellationToken ct)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            owner.Cleanup(stage);
        }
    }

    private string Folder(ItemId? id)
    {
        if (id is not { } folder) { throw new Unsupported(null); }
        var path = Resolve(folder);
        if (!Directory.Exists(path)) { throw Exists(path) ? new Unsupported(id) : new NotFound(id); }
        return path;
    }

    private static string SafeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ')
            || name.Any(character => character < 32 || "<>:\"/\\|?*".Contains(character, StringComparison.Ordinal))
            || name.Equals(TrashFolderName, StringComparison.OrdinalIgnoreCase)) { throw new Denied(null); }
        var stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4 && stem[3] is >= '1' and <= '9'
                && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))))
        { throw new Denied(null); }
        return name;
    }

    private string Destination(string path, ConflictPolicy conflict, string? self)
    {
        _ = Resolve(IdOf(path));
        if (!Exists(path) || self is not null && PathComparer.Equals(path, self)) { return path; }
        return conflict switch
        {
            ConflictPolicy.Fail => throw new Conflict(IdOf(path)),
            ConflictPolicy.Rename => Path.Combine(Path.GetDirectoryName(path)!, StorageWriteRunner.FreeName(Path.GetFileName(path),
                Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)!).Select(Path.GetFileName).OfType<string>())),
            ConflictPolicy.Overwrite => path,
            _ => throw new Unsupported(IdOf(path)),
        };
    }

    private void Publish(string stage, string destination, ConflictPolicy conflict)
    {
        _ = Resolve(IdOf(destination));
        PathConfinement.RequireNoLinks(stage);
        if (!Exists(destination)) { MovePath(stage, destination); return; }
        if (conflict != ConflictPolicy.Overwrite) { throw new Conflict(IdOf(destination)); }
        if (Directory.Exists(stage) != Directory.Exists(destination)) { throw new Conflict(IdOf(destination)); }
        if (File.Exists(stage)) { File.Move(stage, destination, overwrite: true); return; }
        ValidateTree(destination, CancellationToken.None);
        var backup = StagePath(Path.GetDirectoryName(destination)!);
        MovePath(destination, backup);
        try { MovePath(stage, destination); }
        catch
        {
            MovePath(backup, destination);
            throw;
        }
        Cleanup(backup);
    }

    private void Trash(string source)
    {
        source = Resolve(IdOf(source));
        if (IsRoot(source)) { throw new Denied(IdOf(source)); }
        var root = PathConfinement.Containing(AllowedRoots, source);
        var receipt = Path.Combine(PrivateFolder(root), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(receipt);
        try
        {
            File.WriteAllText(Path.Combine(receipt, "receipt.json"), JsonSerializer.Serialize(new TrashReceipt(source, Path.GetFileName(source))));
            _ = Resolve(IdOf(source));
            PathConfinement.RequireNoLinks(receipt);
            MovePath(source, Path.Combine(receipt, Path.GetFileName(source)));
        }
        catch
        {
            Cleanup(receipt);
            throw;
        }
    }

    private string StagePath(string destinationFolder)
    {
        var root = PathConfinement.Containing(AllowedRoots, Resolve(IdOf(destinationFolder)));
        var directory = Path.Combine(PrivateFolder(root), ".staging");
        PathConfinement.RequireNoLinks(directory);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Guid.NewGuid().ToString("N"));
    }

    private string PrivateFolder(string root)
    {
        _ = Resolve(IdOf(root));
        var folder = Path.Combine(root, TrashFolderName);
        PathConfinement.RequireNoLinks(folder);
        if (!Directory.Exists(folder))
        {
            var temporary = Path.Combine(root, ".intochat-working-" + Guid.NewGuid().ToString("N"));
            _coordinator.WorkingFolders.TryAdd(temporary, 0);
            try
            {
                Directory.CreateDirectory(temporary);
                File.WriteAllText(Path.Combine(temporary, ".owner"), Marker);
                _ = Resolve(IdOf(root));
                try { Directory.Move(temporary, folder); }
                catch (IOException error) when (Directory.Exists(folder))
                { _coordinator.Logger.LogSwallowed(error, "Another storage operation created the private working folder."); }
            }
            finally { Cleanup(temporary); _coordinator.WorkingFolders.TryRemove(temporary, out _); }
        }
        var marker = Path.Combine(folder, ".owner");
        PathConfinement.RequireNoLinks(marker);
        if (!File.Exists(marker) || File.ReadAllText(marker) != Marker) { throw new Conflict(IdOf(folder)); }
        return folder;
    }

    private void ValidateTree(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _ = Resolve(IdOf(path));
        if (!Directory.Exists(path)) { return; }
        foreach (var child in Directory.EnumerateFileSystemEntries(path).Where(child => !PrivatePath(child))) { ValidateTree(child, ct); }
    }

    private async Task CopyTree(string source, string target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        source = Resolve(IdOf(source));
        PathConfinement.RequireNoLinks(target);
        if (Directory.Exists(source))
        {
            Directory.CreateDirectory(target);
            foreach (var child in Directory.EnumerateFileSystemEntries(source).Where(child => !PrivatePath(child)))
            { await CopyTree(child, Path.Combine(target, Path.GetFileName(child)), ct).ConfigureAwait(false); }
            return;
        }
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
        await input.CopyToAsync(output, ct).ConfigureAwait(false);
        await output.FlushAsync(ct).ConfigureAwait(false);
        input.Position = 0;
        output.Position = 0;
        var original = await SHA256.HashDataAsync(input, ct).ConfigureAwait(false);
        var copy = await SHA256.HashDataAsync(output, ct).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(original, copy)) { throw new Unavailable(IdOf(source)); }
    }

    private bool IsRoot(string path) => AllowedRoots.Any(root => PathComparer.Equals(PathConfinement.Normalize(root), PathConfinement.Normalize(path)));

    private static bool IsInside(string folder, string path)
        => path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private bool PrivatePath(string path)
        => path.Split(Path.DirectorySeparatorChar).Any(part => part.Equals(TrashFolderName, StringComparison.OrdinalIgnoreCase))
            || _coordinator.WorkingFolders.Keys.Any(folder => PathComparer.Equals(folder, path) || IsInside(folder, path));

    private bool Visible(FileSystemInfo item) => !PrivatePath(item.FullName) && (item.Attributes & FileAttributes.ReparsePoint) == 0;
    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static void MovePath(string source, string target) { if (Directory.Exists(source)) { Directory.Move(source, target); } else { File.Move(source, target); } }
    private static void DeletePath(string path) { if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); } else { File.Delete(path); } }

    private void Cleanup(string path)
    {
        try { PathConfinement.RequireNoLinks(path); DeletePath(path); }
        catch (IOException error) { _coordinator.Logger.LogSwallowed(error, "Storage staging cleanup could not finish."); }
        catch (UnauthorizedAccessException error) { _coordinator.Logger.LogSwallowed(error, "Storage staging cleanup access was denied."); }
    }

    private static string IoCode(IOException error) => (error.HResult & 0xffff) is 0x70 or 0x27 ? "quota-exceeded" : "unavailable";
    private sealed record TrashReceipt(string OriginalPath, string Name);
    private sealed record WindowsStorageOperation(WritePlan Plan, long? Bytes, string? Sha256) : OperationPlan(Plan.DryRun);
    private sealed class SourceRemovalFailed() : StorageException("source-delete-failed", null, null);
}
