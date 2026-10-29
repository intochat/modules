using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Person.Signals;

[Signal("ui.person-changed")]
public sealed record PersonChanged(string Name, int Version) : DigitalBrain.Signal;
