using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Video.Signals;

[Signal("ui.video-changed")]
public sealed record VideoChanged(string Name, bool Playing) : DigitalBrain.Signal;
