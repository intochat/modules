using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Rating.Signals;

[Signal("ui.rating-changed")]
public sealed record RatingChanged(string Name, int Value) : DigitalBrain.Signal;
