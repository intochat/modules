using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Sheet;

[NeuronAlias("sheet"), DefaultNeuronType(UIVocabulary.SheetType)]
public interface ISheet
{
    Task Set(string title, IReadOnlyList<SheetCell> cells);
    [DigitalBrain.ReadOnlyCall] Task<SheetState> Read();
}

public sealed record SheetCell(
    int Row,
    int Column,
    string Value);

[ExportableState]
public sealed class SheetState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public List<SheetCell> Cells { get; set; } = [];
}
