namespace IntoChat.OS.Modules.Microsoft.CSharp.Files;

public enum CSharpFileStatus { Stopped, Starting, Running, Exited }

// A standalone program as its brain sees it: its readable name and purpose, its source, the assembly it compiled to, and the status its
// brain runtime last heard from the sandbox session.
public sealed record CSharpFileSnapshot(
    string Id,
    string Source,
    IReadOnlyDictionary<string, string> Settings,
    string? Hash,
    string Status,
    string? Error,
    bool ShouldRun,
    string Name,
    string Purpose,
    DateTimeOffset? Created);

public sealed record CSharpSubscriptionView(
    string Neuron,
    string Signal,
    int Pending,
    long Delivered,
    long Dropped);
