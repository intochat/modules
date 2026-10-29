namespace IntoChat.OS.Modules.AI.SystemOne;

// Unstructured state plus typed questions. Model overrides the deployment's default System One model.
public sealed record SystemOneRequest(
    string State,
    Dictionary<string, SystemOneQuestion> Questions,
    string? Model = null);
