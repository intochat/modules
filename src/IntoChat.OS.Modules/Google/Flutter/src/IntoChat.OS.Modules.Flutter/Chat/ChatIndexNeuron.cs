using DigitalBrain;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Chat;

[GenerateSerializer, Alias("ui.chat-index-state")]
[ExportableState]
internal sealed record ChatIndexState
{
    [Id(0)] public long Revision { get; init; }
    [Id(1)] public ChatIndexEntry[] Entries { get; init; } = [];
}

[Neuron(ChatIndex.GrainType)]
internal sealed class ChatIndexNeuron
    : UiNeuron<ChatIndexState>, IChatIndex
{
    [DigitalBrain.ReadOnlyCall]
    public Task<ChatIndexPage> List(bool archived, int skip = 0, int take = ChatIndex.MaxPage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, ChatIndex.MaxPage);
        var reachable = Snapshot.Entries.Where(entry => entry.Archived == archived)
            .OrderByDescending(entry => entry.UpdatedAt).ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Take(ChatIndex.MaxListed).ToArray();
        return Task.FromResult(new ChatIndexPage([.. reachable.Skip(skip).Take(take)], reachable.Length));
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<ChatIndexEntry?> Get(string id) => Task.FromResult(Find(ValidId(id)));

    public async Task<ChatIndexEntry> Create(string id, string title)
    {
        ValidId(id);
        var valid = ChatIndex.ValidTitle(title);
        if (Find(id) is { } existing) { return existing; }
        var entry = new ChatIndexEntry(id, valid, Now(), false);
        await Write([.. Snapshot.Entries, entry]);
        return entry;
    }

    public Task Rename(string id, string title)
    {
        var valid = ChatIndex.ValidTitle(title);
        return Update(id, entry => entry.Title == valid ? entry : entry with { Title = valid });
    }

    public Task Archive(string id) => Update(id, entry => entry.Archived ? entry : entry with { Archived = true });

    public Task Unarchive(string id) => Update(id, entry => entry.Archived ? entry with { Archived = false } : entry);

    public async Task Touch(string id, string? firstMessage)
    {
        ValidId(id);
        var at = Now();
        if (Find(id) is not { } entry)
        {
            await Write([.. Snapshot.Entries, new ChatIndexEntry(id, ChatIndex.TitleFrom(firstMessage), at, false)]);
            return;
        }
        var title = entry.Title is ChatIndex.NewChat or ChatIndex.Untitled && firstMessage is not null
            && ChatIndex.TitleFrom(firstMessage) is var from && from != ChatIndex.NewChat ? from : entry.Title;
        await Write(Replace(entry with { Title = title, UpdatedAt = at }));
    }

    public Task SetModel(string id, string? modelChoice)
    {
        if (modelChoice is not null && (string.IsNullOrWhiteSpace(modelChoice) || modelChoice.Length > ChatIndex.ModelChoiceMax
            || modelChoice.Any(char.IsControl)))
        { throw new ArgumentException($"A model choice is null or 1 to {ChatIndex.ModelChoiceMax} characters.", nameof(modelChoice)); }
        return Update(id, entry => entry.ModelChoice == modelChoice ? entry : entry with { ModelChoice = modelChoice });
    }

    private async Task Update(string id, Func<ChatIndexEntry, ChatIndexEntry> change)
    {
        ValidId(id);
        var entry = Find(id) ?? throw new KeyNotFoundException($"No chat '{id}' is indexed.");
        var next = change(entry);
        if (next != entry) { await Write(Replace(next)); }
    }

    private Task Write(ChatIndexEntry[] entries)
    {
        var next = new ChatIndexState { Revision = Snapshot.Revision + 1, Entries = entries };
        return Save(next, new ChatIndexChanged(FullKey, next.Revision));
    }

    private ChatIndexEntry[] Replace(ChatIndexEntry next)
        => [.. Snapshot.Entries.Select(entry => entry.Id == next.Id ? next : entry)];

    private ChatIndexEntry? Find(string id) => Array.Find(Snapshot.Entries, entry => entry.Id == id);

    // Strictly increasing, so entries written in one tick still order newest first.
    private DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        var latest = Snapshot.Entries.Length == 0 ? DateTimeOffset.MinValue : Snapshot.Entries.Max(entry => entry.UpdatedAt);
        return now > latest ? now : latest.AddTicks(1);
    }

    private static string ValidId(string id)
        => ChatIndex.IsId(id) ? id : throw new ArgumentException("A chat id is \"main\" or \"c-\" and 32 hex digits.", nameof(id));
}
