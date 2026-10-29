using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Calendar.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Calendar;

[Neuron(UIVocabulary.CalendarType)]
internal sealed class CalendarNeuron
    : UiNeuron<CalendarState>, ICalendar
{
    public Task Set(string mode, IReadOnlyList<string> selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(selected);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Mode = mode.Trim();
        next.Selected = [.. selected];
        return Save(next, new CalendarChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<CalendarState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
