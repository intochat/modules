using DigitalBrain.Capabilities;
using IntoChat.OS.Framework.Modules;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Files;

// A file's record: its name and purpose, its program, its settings and the caller who started it. The runtime speaks for that
// caller as the owner of the file's one-behavior program. Plain state is JSON (06-06d); Created marks a file that exists.
[ExportableState]
internal sealed record CSharpFileState
{
    public string Program { get; init; } = "";
    public string? Hash { get; init; }
    public bool Wanted { get; init; }
    public CapabilityCaller? Owner { get; init; }
    public string Name { get; init; } = "";
    public string Purpose { get; init; } = "";
    public DateTimeOffset? Created { get; init; }
    public Dictionary<string, string> Settings { get; init; } = new(StringComparer.Ordinal);
}
