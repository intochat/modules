using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Sheet.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Sheet;

[Neuron(UIVocabulary.SheetType)]
internal sealed class SheetNeuron
    : UiNeuron<SheetState>, ISheet
{
    public Task Set(string title, IReadOnlyList<SheetCell> cells)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(cells);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Title = title.Trim();
        next.Cells = [.. cells];
        return Save(next, new SheetChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<SheetState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
