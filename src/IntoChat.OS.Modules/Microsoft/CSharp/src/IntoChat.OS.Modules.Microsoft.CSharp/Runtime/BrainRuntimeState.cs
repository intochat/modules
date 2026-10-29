using DigitalBrain.Capabilities;
using System.Runtime.Serialization;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// A program the brain wants loaded: its compiled synapses, settings and the identity it acts as.
[GenerateSerializer, Alias("microsoft.csharp.program-assignment")]
internal sealed record ProgramAssignment(
    [property: Id(0)] string Owner,
    [property: Id(1)] string Hash,
    [property: Id(2), LegacyShape] Dictionary<string, string>? LegacySettings,
    [property: Id(3)] CapabilityCaller? Principal,
    [property: Id(4)] BehaviorAssembly[] Behaviors,
    [property: Id(5)] StringEntry[] SettingEntries)
{
    // A factory, not a second constructor: the runtime's state is JSON (06-08), which binds one constructor.
    public static ProgramAssignment Of(string owner, string hash, IEnumerable<KeyValuePair<string, string>> settings, CapabilityCaller? principal, BehaviorAssembly[] behaviors)
        => new(owner, hash, null, principal, behaviors, StringEntry.From(settings));

    [IgnoreDataMember, System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, string> Settings => StringEntry.ToMap(SettingEntries, LegacySettings);
}

// One synapse of a program: its path within the program and the assembly it compiled to.
[GenerateSerializer, Alias("microsoft.csharp.behavior-assembly")]
internal sealed record BehaviorAssembly([property: Id(0)] string Path, [property: Id(1)] string Hash)
{
    public string Id(string owner) => BehaviorIds.For(owner, Path);
}

// The one session that runs this brain's behaviors. Started is when it was last launched, so a session
// that never connects is relaunched like one that stopped heartbeating; TokenIssued drives rotation.
[GenerateSerializer, Alias("microsoft.csharp.session-record")]
internal sealed record SessionRecord(
    [property: Id(0)] string Id,
    [property: Id(1)] SessionStatus Status,
    [property: Id(2)] string? Fingerprint,
    [property: Id(3)] DateTimeOffset? LastHeartbeat,
    [property: Id(4)] DateTimeOffset Started,
    [property: Id(5)] DateTimeOffset TokenIssued);

[GenerateSerializer, Alias("microsoft.csharp.runtime-state")]
[ExportableState]
internal sealed record BrainRuntimeState
{
    [Id(0)] public ProgramAssignment[] Programs { get; init; } = [];
    [Id(1)] public Subscription[] Subscriptions { get; init; } = [];
    [Id(2)] public SessionRecord? Session { get; init; }
    [Id(3), LegacyShape] public Dictionary<string, string>? LegacyInitialized { get; init; }
    [Id(4), LegacyShape] public Dictionary<string, string>? LegacyAssignmentRevisions { get; init; }
    [Id(5)] public StringEntry[] InitializedEntries { get; init; } = [];
    [Id(6)] public StringEntry[] AssignmentRevisionEntries { get; init; } = [];

    [IgnoreDataMember, System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, string> Initialized
    {
        get => StringEntry.ToMap(InitializedEntries, LegacyInitialized);
        init { InitializedEntries = StringEntry.From(value); LegacyInitialized = null; }
    }

    [IgnoreDataMember, System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, string> AssignmentRevisions
    {
        get => StringEntry.ToMap(AssignmentRevisionEntries, LegacyAssignmentRevisions);
        init { AssignmentRevisionEntries = StringEntry.From(value); LegacyAssignmentRevisions = null; }
    }
}
