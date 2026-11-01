namespace IntoChat.OS.Modules.AI.Decision;

public sealed record ReactivePrompt(
    string Intent,
    string[] CandidateOperations,
    IReadOnlyDictionary<string, string>? Context = null);
