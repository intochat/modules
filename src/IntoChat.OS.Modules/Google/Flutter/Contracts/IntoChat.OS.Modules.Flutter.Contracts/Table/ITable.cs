using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Table;

[NeuronAlias("table"), DefaultNeuronType(UIVocabulary.TableType)]
public interface ITable
{
    Task Replace(string title, IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows);
    [DigitalBrain.ReadOnlyCall] Task<TableState> Read();
}

public sealed record TableColumn(string Id, string Title);

[ExportableState]
public sealed class TableState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public List<TableColumn> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
}
