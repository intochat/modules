using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Expander.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Expander;

[Neuron(UIVocabulary.ExpanderType)]
internal sealed class ExpanderNeuron
    : UiNeuron<ExpanderState>, IExpander
{
    public Task Set(string header, bool expanded, IReadOnlyList<UiChildRef>? children = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Header = header.Trim();
        next.Expanded = expanded;
        next.Children = [.. children ?? []];
        return Save(next, new ExpanderChanged(FullKey, next.Expanded));
    }

    public Task Toggle()
    {
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Expanded = !next.Expanded;
        return Save(next, new ExpanderChanged(FullKey, next.Expanded));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ExpanderState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
