using DigitalBrain;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.Memory;

[ExportableState]
public sealed record MemoryState(
    long Revision,
    MemoryEpisode[] Episodes,
    MemoryFact[] Facts)
{
    public MemoryState() : this(0, [], []) { }
}

[Neuron("ai.memory", State = "memory")]
public sealed class MemoryNeuron : Neuron<MemoryState>, IMemory
{
    private const int MaxEpisodes = 256;
    private const int MaxFacts = 512;

    public Task<MemorySnapshot> Read()
        => Task.FromResult(new MemorySnapshot(State.Revision, State.Episodes, State.Facts));

    public async Task<MemoryEpisode> CaptureEpisode(string agent, string summary, int importance = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        var clamped = Math.Clamp(importance, 1, 10);
        var episode = new MemoryEpisode(
            Guid.NewGuid().ToString("N"),
            agent.Trim(),
            summary.Trim(),
            clamped,
            Context.Time.GetUtcNow());
        var episodes = State.Episodes.Append(episode).TakeLast(MaxEpisodes).ToArray();
        var next = new MemoryState(State.Revision + 1, episodes, State.Facts);
        await Save(next, new MemoryChanged(next.Revision, next.Episodes.Length, next.Facts.Length)).ConfigureAwait(false);
        return episode;
    }

    public async Task<MemoryFact> RememberFact(string subject, string text, int importance = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var clamped = Math.Clamp(importance, 1, 10);
        var fact = new MemoryFact(
            Guid.NewGuid().ToString("N"),
            subject.Trim(),
            text.Trim(),
            clamped,
            Context.Time.GetUtcNow());
        var existing = State.Facts
            .Where(f => !string.Equals(f.Subject, fact.Subject, StringComparison.OrdinalIgnoreCase)
                     || !string.Equals(f.Text, fact.Text, StringComparison.OrdinalIgnoreCase))
            .Append(fact)
            .TakeLast(MaxFacts)
            .ToArray();
        var next = new MemoryState(State.Revision + 1, State.Episodes, existing);
        await Save(next, new MemoryChanged(next.Revision, next.Episodes.Length, next.Facts.Length)).ConfigureAwait(false);
        return fact;
    }

    public Task<MemoryRecallHit[]> Recall(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Task.FromResult<MemoryRecallHit[]>([]);
        }

        var terms = Tokenize(query);
        var hits = new List<MemoryRecallHit>();

        foreach (var fact in State.Facts)
        {
            var score = Score($"{fact.Subject} {fact.Text}", terms, fact.Importance);
            if (score > 0)
            {
                hits.Add(new MemoryRecallHit(fact.Id, "fact", $"{fact.Subject}: {fact.Text}", score));
            }
        }

        foreach (var episode in State.Episodes)
        {
            var score = Score($"{episode.Agent} {episode.Summary}", terms, episode.Importance);
            if (score > 0)
            {
                hits.Add(new MemoryRecallHit(episode.Id, "episode", episode.Summary, score));
            }
        }

        var ranked = hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
        return Task.FromResult(ranked);
    }

    private static string[] Tokenize(string text)
        => text.Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '"', '\'', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 2)
            .Select(w => w.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static double Score(string document, string[] queryTerms, int importance)
    {
        if (queryTerms.Length == 0) { return 0; }
        var matched = queryTerms.Count(term => document.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (matched == 0) { return 0; }
        var lexical = (double)matched / queryTerms.Length;
        return lexical * (1.0 + (importance / 20.0));
    }
}
