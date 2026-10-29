using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Preferences;

// Where a preference lives. The first block is the brain's {brain}/settings text fields, which any member may
// change; the Local* block is this device's own choice, which the renderer reads and writes on the device.
public enum PreferenceStore
{
    NotifyAgentFinished = 0,
    NotifyNeedsApproval = 1,
    NotifyConnectionErrors = 2,
    NotifyProductUpdates = 3,
    NewTabApp = 4,
    LocalDisplayName = 10,
    LocalTheme = 11,
    LocalCompactSpacing = 12,
    LocalReducedMotion = 13,
    LocalDefaultModel = 14,
}

public enum PreferenceKind { Toggle = 0, Choice = 1, Text = 2 }

public sealed record PreferenceChoice(
    string Id,
    string Label,
    bool Enabled = true,
    string? Detail = null);

public sealed record PreferenceSpec(
    PreferenceStore Store,
    string Label,
    PreferenceKind Kind,
    PreferenceChoice[]? Choices = null);

[NeuronAlias("preference"), DefaultNeuronType(UIVocabulary.PreferenceType)]
public interface IPreferenceControl
{
    Task Configure(PreferenceSpec spec);
    // The spec plus the stored value; Value is null for a Local* store, which only the device holds.
    [DigitalBrain.ReadOnlyCall] Task<PreferenceControlState> Read();
    // Writes a brain store as the calling person. Installed apps are refused; Local* stores are never written here.
    Task Set(string value);
}

[ExportableState]
public sealed class PreferenceControlState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public PreferenceSpec? Spec { get; set; }
    public string? Value { get; set; }
}
