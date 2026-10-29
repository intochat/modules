using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Preferences.Signals;

[Signal("ui.preference-changed")]
public sealed record PreferenceChanged(PreferenceStore Store) : DigitalBrain.Signal;
