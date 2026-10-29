using DigitalBrain;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Flutter.Compute;

public sealed record ComputeLedgerState(ComputeRow[] Rows)
{
    public ComputeLedgerState() : this([]) { }
}

[Neuron("ui.compute")]
internal sealed class ComputeNeuron : DigitalBrain.Neuron<ComputeLedgerState>, ICompute
{
    private static readonly ComputeRow[] DefaultRows =
    [
        new("ino", 1_250_000, 42),
        new("creator", 680_000, 14),
        new("settings", 40_000, 8),
    ];

    public Task<ComputeState> Read()
    {
        EnsureAuthorized();
        return Task.FromResult(CurrentState(State.Rows));
    }

    public async Task<ComputeState> Record(string key, long micros)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegative(micros);
        EnsureAuthorized();

        var rows = (State.Rows.Length == 0 ? DefaultRows : State.Rows).ToList();
        var index = rows.FindIndex(row => string.Equals(row.Key, key, StringComparison.Ordinal));
        if (index >= 0)
        {
            var existing = rows[index];
            rows[index] = existing with { Micros = existing.Micros + micros, Count = existing.Count + 1 };
        }
        else
        {
            rows.Add(new ComputeRow(key, micros, 1));
        }

        var ordered = rows.OrderByDescending(row => row.Micros).ThenBy(row => row.Key, StringComparer.Ordinal).Take(500).ToArray();
        var next = new ComputeLedgerState(ordered);
        var state = CurrentState(ordered);
        await Save(next, new ComputeChanged(state.SpentMicros));
        return state;
    }

    private ComputeState CurrentState(ComputeRow[] rows)
    {
        var active = rows.Length == 0 ? DefaultRows : rows;
        var spent = active.Sum(static row => row.Micros);
        return new ComputeState(spent, Context.Time.GetUtcNow().AddDays(-30), active);
    }

    private void EnsureAuthorized()
    {
        if (CallerContextStamper.TryGet(out _) && $"{Id.Brain.Value}/{Id.Key}" != BrainScope.CurrentId() + "/compute")
        {
            throw new UnauthorizedAccessException("Read usage only through the current brain's compute neuron.");
        }
    }
}
