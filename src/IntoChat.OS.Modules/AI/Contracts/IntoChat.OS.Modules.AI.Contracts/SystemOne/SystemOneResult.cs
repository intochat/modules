namespace IntoChat.OS.Modules.AI.SystemOne;

// One entry in Answers per question id from the request.
public sealed record SystemOneResult(
    string Model,
    Dictionary<string, SystemOneAnswer> Answers,
    int InputTokens = 0,
    int OutputTokens = 0);
