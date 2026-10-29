using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Calendar;

[NeuronAlias("calendar"), DefaultNeuronType(UIVocabulary.CalendarType)]
public interface ICalendar
{
    Task Set(string mode, IReadOnlyList<string> selected);
    [DigitalBrain.ReadOnlyCall] Task<CalendarState> Read();
}

[ExportableState]
public sealed class CalendarState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Mode { get; set; } = "day";
    public List<string> Selected { get; set; } = [];
}
