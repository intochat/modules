using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Tabs;

[NeuronAlias("tabs"), DefaultNeuronType(UIVocabulary.TabsType)]
public interface ITabs
{
    Task Set(IReadOnlyList<TabItem> tabs, string selectedId);
    Task Select(string id);
    [DigitalBrain.ReadOnlyCall] Task<TabsState> Read();
}

public sealed record TabItem(
    string Id,
    string Title,
    UiChildRef Child);

[ExportableState]
public sealed class TabsState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public List<TabItem> Tabs { get; set; } = [];
    public string SelectedId { get; set; } = "";
}
