namespace IntoChat.OS.Modules.Microsoft.Playwright;

public sealed record BrowserAttachment(int Port, string SessionId);
public sealed record BrowserSession(bool Connected, string? SessionId,
    string? Url, string? Title, bool Ready = false);
public sealed record BrowserObservation(string Url, string Title,
    string Text, BrowserLink[] Links);
public sealed record BrowserLink(string Text, string Url);
