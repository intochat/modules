using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Form.Signals;

[Signal("ui.form-changed")]
public sealed record FormChanged(
    string FormId,
    int Revision) : DigitalBrain.Signal;
