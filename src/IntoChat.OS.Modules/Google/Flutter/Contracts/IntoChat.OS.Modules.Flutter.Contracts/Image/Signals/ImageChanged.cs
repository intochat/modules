using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Image.Signals;

[Signal("ui.image-changed")]
public sealed record ImageChanged(string Name, int Version) : DigitalBrain.Signal;
