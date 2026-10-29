using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Table.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Table;

[Neuron(UIVocabulary.TableType)]
internal sealed class TableNeuron
    : UiNeuron<TableState>, ITable
{
    public Task Replace(string title, IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Title = title.Trim();
        next.Columns = [.. columns];
        next.Rows = [.. rows.Select(row => row.ToList())];
        return Save(next, new TableChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<TableState> Read()
    {
        var state = Snapshot;
        state.Name = FullKey;
        return Task.FromResult(state);
    }
}
