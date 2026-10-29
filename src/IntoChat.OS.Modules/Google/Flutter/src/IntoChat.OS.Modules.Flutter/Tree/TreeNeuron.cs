using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Tree.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Tree;

[Neuron(UIVocabulary.TreeType)]
internal sealed class TreeNeuron
    : UiNeuron<TreeState>, ITree
{
    public Task Set(IReadOnlyList<TreeNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Nodes = [.. nodes];
        if (!next.Nodes.Exists(node => node.Id == next.SelectedId))
        {
            next.SelectedId = next.Nodes.Count > 0 ? next.Nodes[0].Id : "";
        }

        return Save(next, new TreeChanged(FullKey, next.Version));
    }

    public Task Select(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var next = Snapshot;
        if (!next.Nodes.Exists(node => node.Id == id)) { throw new ArgumentException("Unknown node.", nameof(id)); }
        next.Name = FullKey;
        next.Version++;
        next.SelectedId = id;
        return Save(next, new TreeChanged(FullKey, next.Version), new TreeSelected(FullKey, id));
    }

    [DigitalBrain.ReadOnlyCall] public Task<TreeState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
