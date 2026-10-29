using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Card.Signals;

[Signal("ui.card-changed")]
public sealed record CardChanged(string Name, int Version) : DigitalBrain.Signal;
