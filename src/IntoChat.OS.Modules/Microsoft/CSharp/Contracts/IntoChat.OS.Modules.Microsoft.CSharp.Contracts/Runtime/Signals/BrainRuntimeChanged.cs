using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;

// Published on every durable change of a brain runtime: its behaviors, subscriptions or session.
[DigitalBrain.Signal("microsoft.csharp.runtime-changed")]
public sealed record BrainRuntimeChanged(string RuntimeId) : DigitalBrain.Signal;
