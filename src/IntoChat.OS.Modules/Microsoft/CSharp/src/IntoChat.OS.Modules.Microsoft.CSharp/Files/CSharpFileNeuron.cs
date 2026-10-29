using System.Text;
using System.Text.RegularExpressions;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Files.Signals;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Files;

// A file is one standalone program of its brain's runtime: its source compiles to a single behavior whose
// id is the file's own key. Running it, its subscriptions and its restarts belong to the runtime's one
// sandbox session. A plain neuron since 06-06d.
[DigitalBrain.Neuron("microsoft.csharp.file")]
internal sealed partial class CSharpFileNeuron : DigitalBrain.Neuron<CSharpFileState>, ICSharpFile
{
    internal const int MaximumSourceBytes = 128 * 1024;
    private const int MaximumSettings = 64;
    // A deleted file's record is gone; the base keeps the last state it saved in memory, so the file reads empty until it is written again.
    private bool _cleared;
    private static readonly CSharpFileState Empty = new();

    private CSharpFileState Current => _cleared ? Empty : State;

    // The os key "{brain}/{key}" the file's behavior id and signals carry, as the grain's key did.
    private string FileId => $"{Id.Brain.Value}/{Id.Key}";

    private bool Exists => Current.Created is not null;

    // The key leads with its brain (CSharpFileKeys), so the runtime is found without a stamped caller.
    private string RuntimeKey
    {
        get
        {
            if (!Id.Key.Contains('/', StringComparison.Ordinal)) { throw new InvalidOperationException($"'{FileId}' is not a behavior key; use CSharpFileKeys.For(brain, name)."); }
            return BrainRuntimeKeys.For(Id.Brain.Value);
        }
    }

    // The runtime is a plain neuron of this brain (06-08), reached as this file.
    private IBrainPrograms Programs => Get<IBrainPrograms>(RuntimeId);

    private DigitalBrain.NeuronId RuntimeId
    {
        get
        {
            _ = RuntimeKey;
            return new(Id.Brain, BrainRuntimeNeuron.GrainTypeName, BrainRuntimeKeys.Name);
        }
    }

    public async Task<CSharpFileSnapshot> Inspect(CancellationToken cancellationToken = default)
        => View(Current.Wanted ? await Programs.Describe(FileId) : null);

    public async Task<CSharpFileSnapshot> Describe(string? name, string? purpose, CancellationToken cancellationToken = default)
    {
        if (name is { Length: > 120 } || purpose is { Length: > 4000 })
        { throw new ArgumentException("A name is at most 120 characters and a purpose at most 4000."); }
        // Describing names a file that exists; it never creates one.
        if (!Exists) { throw new KeyNotFoundException($"No C# file '{FileId}' exists; write it first."); }
        await Keep(Current with
        {
            Name = string.IsNullOrWhiteSpace(name) ? Current.Name is { Length: > 0 } current ? current : FileId[(FileId.LastIndexOf('/') + 1)..] : name.Trim(),
            Purpose = purpose?.Trim() ?? Current.Purpose,
        });
        return await Inspect(cancellationToken);
    }

    public Task Write(string source, CancellationToken cancellationToken = default)
    {
        RequireSize(source);
        return Keep(Current with { Program = source });
    }

    public Task Configure(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Count > MaximumSettings) { throw new ArgumentException($"At most {MaximumSettings} settings are allowed.", nameof(settings)); }
        if (settings.Keys.FirstOrDefault(name => !SettingName().IsMatch(name)) is { } invalid)
        { throw new ArgumentException($"Setting '{invalid}' must contain only letters, digits and underscores.", nameof(settings)); }
        return Keep(Current with { Settings = new(settings, StringComparer.Ordinal) });
    }

    public Task<CSharpFileSnapshot> Start(CancellationToken cancellationToken = default) => AssignAsync(Current.Program);

    public Task<CSharpFileSnapshot> Swap(string source, CancellationToken cancellationToken = default)
    {
        RequireSize(source);
        return AssignAsync(source);
    }

    public async Task<CSharpFileSnapshot> Stop(CancellationToken cancellationToken = default)
    {
        await Programs.Retire(FileId);
        await Keep(Current with { Wanted = false });
        return View(null);
    }

    public async Task Delete(CancellationToken cancellationToken = default)
    {
        if (Id.Key.Contains('/', StringComparison.Ordinal)) { await Programs.Retire(FileId); }
        await Context.State.Clear();
        _cleared = true;
        await Publish(new CSharpFileChanged(FileId));
    }

    // The runtime's edge face is internal to this module; the log read goes to the runtime neuron of this brain.
    public Task<string> ReadLogs(int tail = 200, CancellationToken cancellationToken = default)
        => Current.Wanted ? Get<IBrainRuntimeEdge>(RuntimeId).Logs(FileId, tail) : Task.FromResult("");

    // The runtime compiles; a source that does not compile throws out of Assign and nothing is recorded.
    private async Task<CSharpFileSnapshot> AssignAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) { throw new InvalidOperationException("Write the C# source before starting it."); }
        var program = await Programs.Assign(FileId, new(StringComparer.Ordinal) { [""] = source }, new(Current.Settings, StringComparer.Ordinal));
        var owner = CallerContextStamper.TryGet(out var caller) ? caller : Current.Owner;
        await Keep(Current with { Program = source, Hash = program.Behaviors[0].Hash, Wanted = true, Owner = owner });
        return View(program);
    }

    // The first write of any kind is the file's creation.
    private async Task Keep(CSharpFileState next)
    {
        next = next with { Created = next.Created ?? Context.Time.GetUtcNow() };
        await Save(next, new CSharpFileChanged(FileId));
        _cleared = false;
    }

    private static void RequireSize(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (Encoding.UTF8.GetByteCount(source) > MaximumSourceBytes) { throw new ArgumentException($"Source exceeds {MaximumSourceBytes} bytes.", nameof(source)); }
    }

    private CSharpFileSnapshot View(ProgramView? program)
    {
        var behavior = program?.Behaviors.FirstOrDefault();
        return new(FileId, Current.Program, Current.Settings, Current.Hash, behavior?.Status ?? "Stopped", behavior?.Error, Current.Wanted, Current.Name, Current.Purpose, Current.Created);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,128}$")]
    private static partial Regex SettingName();
}
