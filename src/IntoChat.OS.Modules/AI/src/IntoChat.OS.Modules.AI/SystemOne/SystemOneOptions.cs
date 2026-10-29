namespace IntoChat.OS.Modules.AI;

// The deployment's System One (TypeSafe Jev) defaults. Endpoint may point at the hosted API or a
// local container exposing the same /v1/systemone shape; a Ready integration's Endpoint overrides it.
public sealed class SystemOneOptions
{
    public string Endpoint { get; set; } = "https://api.typesafe.ai";
    public string Model { get; set; } = "jev-latest";
}
