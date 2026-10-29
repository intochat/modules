using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Video.Signals;

[Signal("ui.video-ended")]
public sealed record VideoEnded(string Name) : DigitalBrain.Signal;
