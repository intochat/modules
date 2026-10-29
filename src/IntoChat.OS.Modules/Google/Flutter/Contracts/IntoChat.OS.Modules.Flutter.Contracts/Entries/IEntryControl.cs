using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Entries;

public enum EntryControlMode { Key = 0, Collection = 1 }

// Which entries the control edits. It names an entry, never a value: values travel only from the renderer's secret
// field to PUT /brains/{brainId}/entries/{name} over the person's session, which is owner-only.
public sealed record EntryControlSpec(
    EntryControlMode Mode,
    string Label,
    string? EntryName = null);

[NeuronAlias("entrycontrol"), DefaultNeuronType(UIVocabulary.EntryControlType)]
public interface IEntryControl
{
    Task Configure(EntryControlSpec spec);
    [DigitalBrain.ReadOnlyCall] Task<EntryControlState> Read();
    // The renderer calls this after a successful write so the app can refresh; the signal carries only the neuron name.
    Task Changed();
}

[ExportableState]
public sealed class EntryControlState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public EntryControlSpec? Spec { get; set; }
}
