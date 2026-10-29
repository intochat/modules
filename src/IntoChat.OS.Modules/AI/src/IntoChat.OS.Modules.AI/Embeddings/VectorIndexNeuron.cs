using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.AI.Embeddings.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.Embeddings;

[GenerateSerializer, Alias("ai.vector-index-state")]
[ExportableState]
internal sealed record VectorIndexState
{
    [Id(0)] public VectorEntry[] Entries { get; init; } = [];
    [Id(1)] public int Dimensions { get; init; }
    [Id(2)] public long Revision { get; init; }
}

// Every write rewrites the whole record, so the index is bounded: 1024 entries of 1536 floats is about 6 MB.
[Neuron("ai.vector-index")]
internal sealed class VectorIndexNeuron
    : AiNeuron<VectorIndexState>, IVectorIndex
{
    internal const int MaxEntries = 1024;
    internal const int MaxDimensions = 4096;
    internal const int MaxPayloadFields = 32;
    internal const int MaxPayloadValueLength = 4096;

    public Task<VectorIndexSnapshot> Replace(VectorEntry[] entries)
    {
        var given = Validate(entries);
        if (given.Length > MaxEntries) { throw new ArgumentException($"An index holds at most {MaxEntries} entries; {given.Length} were given.", nameof(entries)); }
        var dimensions = given.Length == 0 ? 0 : given[0].Vector.Length;
        foreach (var entry in given) { RequireDimensions(entry, dimensions); }
        return Write(given, dimensions);
    }

    public Task<VectorIndexSnapshot> Upsert(VectorEntry[] entries)
    {
        var given = Validate(entries);
        var current = Snapshot;
        var dimensions = current.Entries.Length > 0 ? current.Dimensions : given.Length > 0 ? given[0].Vector.Length : 0;
        foreach (var entry in given) { RequireDimensions(entry, dimensions); }
        var updates = given.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var stored = current.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        VectorEntry[] next =
        [
            .. current.Entries.Select(entry => updates.GetValueOrDefault(entry.Id) ?? entry),
            .. given.Where(entry => !stored.Contains(entry.Id)),
        ];
        if (next.Length > MaxEntries) { throw new InvalidOperationException($"An index holds at most {MaxEntries} entries; this upsert would make it {next.Length}."); }
        return Write(next, dimensions);
    }

    public Task<VectorIndexSnapshot> Remove(string[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var removed = ids.OfType<string>().ToHashSet(StringComparer.Ordinal);
        VectorEntry[] next = [.. Snapshot.Entries.Where(entry => !removed.Contains(entry.Id))];
        return Write(next, next.Length == 0 ? 0 : Snapshot.Dimensions);
    }

    public Task<VectorMatch[]> Nearest(float[] query, int take, Dictionary<string, string>? mustMatch = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        var current = Snapshot;
        if (current.Entries.Length == 0) { return Task.FromResult<VectorMatch[]>([]); }
        if (query.Length != current.Dimensions)
        { throw new ArgumentException($"The index holds {current.Dimensions}-dimensional vectors; the query has {query.Length}.", nameof(query)); }
        RequireFinite(query, "The query");
        var conditions = mustMatch ?? [];
        VectorMatch[] matches =
        [
            .. current.Entries
                .Where(entry => conditions.All(condition => entry.Payload.TryGetValue(condition.Key, out var value) && value == condition.Value))
                .Select(entry => new VectorMatch(entry.Id, Cosine(entry.Vector, query), new(entry.Payload, StringComparer.Ordinal)))
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Id, StringComparer.Ordinal)
                .Take(take),
        ];
        return Task.FromResult(matches);
    }

    public Task<VectorIndexSnapshot> Read() => Task.FromResult(View(Snapshot));

    // An unchanged index is not written again, so a redelivered write leaves the revision where it was.
    private async Task<VectorIndexSnapshot> Write(VectorEntry[] entries, int dimensions)
    {
        var current = Snapshot;
        if (dimensions == current.Dimensions && Same(current.Entries, entries)) { return View(current); }
        var next = new VectorIndexState { Entries = entries, Dimensions = dimensions, Revision = current.Revision + 1 };
        await Save(next, new VectorIndexChanged(FullKey, next.Revision, entries.Length));
        return View(next);
    }

    private static VectorIndexSnapshot View(VectorIndexState state) => new(state.Entries.Length, state.Dimensions, state.Revision);

    // A copy of each entry, so nothing the caller still holds can change what is stored.
    private static VectorEntry[] Validate(VectorEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var copies = new VectorEntry[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index] ?? throw new ArgumentException("An entry is missing.", nameof(entries));
            if (string.IsNullOrWhiteSpace(entry.Id)) { throw new ArgumentException("Every entry needs an id.", nameof(entries)); }
            if (!ids.Add(entry.Id)) { throw new ArgumentException($"Entry '{entry.Id}' is given more than once.", nameof(entries)); }
            if (entry.Vector is not { Length: > 0 } vector) { throw new ArgumentException($"Entry '{entry.Id}' has no vector.", nameof(entries)); }
            if (vector.Length > MaxDimensions) { throw new ArgumentException($"Entry '{entry.Id}' has {vector.Length} dimensions; an index holds at most {MaxDimensions}.", nameof(entries)); }
            RequireFinite(vector, $"Entry '{entry.Id}'");
            var payload = entry.Payload ?? [];
            if (payload.Count > MaxPayloadFields) { throw new ArgumentException($"Entry '{entry.Id}' has {payload.Count} payload fields; at most {MaxPayloadFields} are kept.", nameof(entries)); }
            if (payload.Any(field => field.Value is null || field.Value.Length > MaxPayloadValueLength))
            { throw new ArgumentException($"Entry '{entry.Id}' has a payload value that is missing or longer than {MaxPayloadValueLength} characters.", nameof(entries)); }
            copies[index] = new VectorEntry(entry.Id, [.. vector], new Dictionary<string, string>(payload, StringComparer.Ordinal));
        }
        return copies;
    }

    private static void RequireDimensions(VectorEntry entry, int dimensions)
    {
        if (entry.Vector.Length != dimensions)
        { throw new ArgumentException($"The index holds {dimensions}-dimensional vectors; entry '{entry.Id}' has {entry.Vector.Length}.", "entries"); }
    }

    private static void RequireFinite(float[] vector, string what)
    {
        if (vector.Any(value => !float.IsFinite(value))) { throw new ArgumentException($"{what} has a value that is not a finite number."); }
    }

    private static bool Same(VectorEntry[] left, VectorEntry[] right) =>
        left.Length == right.Length && left.Zip(right).All(pair => pair.First.Id == pair.Second.Id
            && pair.First.Vector.AsSpan().SequenceEqual(pair.Second.Vector)
            && pair.First.Payload.Count == pair.Second.Payload.Count
            && pair.First.Payload.All(field => pair.Second.Payload.TryGetValue(field.Key, out var value) && value == field.Value));

    private static double Cosine(float[] left, float[] right)
    {
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }
        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / Math.Sqrt(leftNorm * rightNorm);
    }
}
