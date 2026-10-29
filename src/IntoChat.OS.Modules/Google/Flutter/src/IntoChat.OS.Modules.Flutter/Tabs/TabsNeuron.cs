using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Tabs.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Tabs;

[Neuron(UIVocabulary.TabsType)]
internal sealed class TabsNeuron
    : UiNeuron<TabsState>, ITabs
{
    public Task Set(IReadOnlyList<TabItem> tabs, string selectedId)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedId);
        if (!tabs.Any(tab => tab.Id == selectedId)) { throw new ArgumentException("Selected tab is not in the set.", nameof(selectedId)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Tabs = [.. tabs];
        next.SelectedId = selectedId;
        return Save(next, new TabsChanged(FullKey, next.SelectedId));
    }

    public Task Select(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var next = Snapshot;
        if (!next.Tabs.Exists(tab => tab.Id == id)) { throw new ArgumentException("Unknown tab.", nameof(id)); }
        next.Name = FullKey;
        next.Version++;
        next.SelectedId = id;
        return Save(next, new TabsChanged(FullKey, next.SelectedId));
    }

    [DigitalBrain.ReadOnlyCall] public Task<TabsState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
