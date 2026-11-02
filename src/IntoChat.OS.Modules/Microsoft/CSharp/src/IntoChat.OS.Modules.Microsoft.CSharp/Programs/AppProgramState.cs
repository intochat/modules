using System.Runtime.Serialization;
using DigitalBrain.Capabilities;
using DigitalBrain.State;
using IntoChat.OS.Connections;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Programs;

// Persisted state of an app program ({brain}/app/{name}).
[GenerateSerializer, Alias("microsoft.csharp.app-program-state")]
[ExportableState]
internal sealed record AppProgramState
{
    [Id(0)] public string AppName { get; init; } = "";
    [Id(1)] public string AppKey { get; init; } = "";
    [Id(2)] public string CompiledSha { get; init; } = "";
    [Id(3)] public CapabilityCaller? Principal { get; init; }
    [Id(4)] public StringEntry[] SettingEntries { get; init; } = [];
    [Id(5)] public BehaviorAssembly[] Behaviors { get; init; } = [];
    [Id(6)] public AppProgramStatus Status { get; init; } = AppProgramStatus.Drafted;
    [Id(7)] public long SignalCursor { get; init; } = 0;
    [Id(8)] public long Watermark { get; init; } = 0;
    [Id(9)] public long CpuMicroseconds { get; init; } = 0;
    [Id(10)] public long WallMicroseconds { get; init; } = 0;
    [Id(11)] public DateTimeOffset? LastAcknowledgedAt { get; init; }
    [Id(12)] public string Logs { get; init; } = "";
    [Id(13)] public string? Error { get; init; }

    [IgnoreDataMember, System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, string> Settings
    {
        get => StringEntry.ToMap(SettingEntries, null);
        init => SettingEntries = StringEntry.From(value);
    }
}
