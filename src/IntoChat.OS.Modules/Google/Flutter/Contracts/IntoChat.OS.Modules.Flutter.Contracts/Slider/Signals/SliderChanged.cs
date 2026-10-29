using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Slider.Signals;

[Signal("ui.slider-changed")]
public sealed record SliderChanged(string Name, double Value) : DigitalBrain.Signal;
