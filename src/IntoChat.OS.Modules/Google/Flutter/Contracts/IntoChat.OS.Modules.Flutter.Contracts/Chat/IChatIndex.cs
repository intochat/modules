using DigitalBrain.Contracts;
using DigitalBrain;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IntoChat.OS.Modules.Flutter.Chat;

// One entry per conversation of an assistant, at `{DataKey}/chats`. Each conversation stays an agent at
// `{DataKey}/conversations/{Id}`; the index only names, orders and archives them.
[NeuronAlias("chat-index"), DefaultNeuronType(ChatIndex.GrainType)]
public interface IChatIndex
{
    // Unarchived (archived: false) or archived (archived: true) entries, newest first; only the newest MaxListed are reachable.
    [DigitalBrain.ReadOnlyCall] Task<ChatIndexPage> List(bool archived, int skip = 0, int take = ChatIndex.MaxPage);
    // Idempotent by id: an existing entry is returned unchanged.
    Task<ChatIndexEntry> Create(string id, string title);
    Task Rename(string id, string title);
    Task Archive(string id);
    Task Unarchive(string id);
    // Marks activity, creating the entry when missing; titles a still-default entry from the first message.
    Task Touch(string id, string? firstMessage);
    Task SetModel(string id, string? modelChoice);
    [DigitalBrain.ReadOnlyCall] Task<ChatIndexEntry?> Get(string id);
}

public sealed record ChatIndexEntry(
    string Id,
    string Title,
    DateTimeOffset UpdatedAt,
    bool Archived,
    string? ModelChoice = null);

public sealed record ChatIndexPage(ChatIndexEntry[] Entries, int Total);

[Signal("ui.chat-index-changed")]
public sealed record ChatIndexChanged(string Name, long Revision) : DigitalBrain.Signal;

public static partial class ChatIndex
{
    public const string GrainType = "chat-index";
    public const int MaxListed = 500;
    public const int MaxPage = 100;
    public const int TitleClip = 52;
    public const int TitleMax = 120;
    public const int ModelChoiceMax = 512;
    public const string NewChat = "New chat";
    public const string Untitled = "Untitled";

    [GeneratedRegex("^(main|c-[0-9a-f]{32})$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    public static bool IsId(string? id) => id is not null && IdPattern().IsMatch(id);

    // A title taken from a message: whitespace runs become one space; control and invisible format characters
    // (bidi overrides, zero-width marks, BOM) are dropped, except a zero-width joiner inside a visible cluster;
    // the result is clipped to TitleClip without splitting a text element. No visible character gives "New chat".
    public static string TitleFrom(string? message)
    {
        if (message is null) { return NewChat; }
        var text = new StringBuilder(Math.Min(message.Length, TitleClip * 4));
        var space = false;
        for (var at = 0; at < message.Length; at++)
        {
            var c = message[at];
            if (char.IsWhiteSpace(c)) { space = text.Length > 0; continue; }
            if (char.IsControl(c) || (Invisible(c) && !Joins(message, at))) { continue; }
            if (space) { text.Append(' '); space = false; }
            text.Append(c);
            if (text.Length > TitleClip * 4) { break; }
        }
        var title = Clip(text.ToString(), TitleClip).TrimEnd();
        return title.Length == 0 ? NewChat : title;
    }

    // A title a person typed: trimmed, one line, no control or invisible format characters, 1 to TitleMax characters.
    public static string ValidTitle(string? title)
    {
        var trimmed = title?.Trim() ?? "";
        if (trimmed.Length is 0 or > TitleMax || trimmed.Any(char.IsControl)
            || Enumerable.Range(0, trimmed.Length).Any(at => Invisible(trimmed[at]) && !Joins(trimmed, at)))
        { throw new ArgumentException($"A chat title is one line of 1 to {TitleMax} visible characters.", nameof(title)); }
        return trimmed;
    }

    private static bool Invisible(char c) => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format;

    // A zero-width joiner between two visible characters (an emoji sequence) is part of what is shown.
    private static bool Joins(string text, int at)
        => text[at] == '‍' && at > 0 && at < text.Length - 1 && Visible(text[at - 1]) && Visible(text[at + 1]);

    private static bool Visible(char c) => !char.IsWhiteSpace(c) && !char.IsControl(c) && !Invisible(c);

    private static string Clip(string text, int limit)
    {
        if (text.Length <= limit) { return text; }
        var length = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            if (length + element.Length > limit) { break; }
            length += element.Length;
        }
        return text[..length];
    }
}
