using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Tree;

[NeuronAlias("tree"), DefaultNeuronType(UIVocabulary.TreeType)]
public interface ITree
{
    Task Set(IReadOnlyList<TreeNode> nodes);
    Task Select(string id);
    [DigitalBrain.ReadOnlyCall] Task<TreeState> Read();
}

public sealed record TreeNode(
    string Id,
    string? ParentId,
    string Label);

[ExportableState]
public sealed class TreeState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public List<TreeNode> Nodes { get; set; } = [];
    public string SelectedId { get; set; } = "";
}
