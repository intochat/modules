using IntoChat.OS.Modules.Microsoft.CSharp.Files;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Authoring;

// The authoring wire shape: the file's short id with its name and purpose, read from the file neuron itself.
public sealed record CSharpDescription(string Id, string Name, string Purpose, DateTimeOffset CreatedAt);

public sealed record CSharpFileView(CSharpDescription Description, CSharpFileSnapshot File, string? Logs = null)
{
    public static CSharpFileView Of(string id, CSharpFileSnapshot file, string? logs = null)
        => new(new(id, file.Name, file.Purpose, file.Created ?? default), file, logs);
}
