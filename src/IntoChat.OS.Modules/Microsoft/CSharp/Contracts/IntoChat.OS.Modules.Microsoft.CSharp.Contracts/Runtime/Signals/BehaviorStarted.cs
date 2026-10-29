using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;

// A batch reached a running synapse: published once per subscription and watermark, by the brain's runs
// neuron. Hash is the synapse's own assembly hash; the stamped Trace is the batch's.
[DigitalBrain.Signal("microsoft.csharp.behavior-started")]
public sealed record BehaviorStarted(
    string RuntimeId, string Owner, string Behavior,
    string Hash, string Source, string SignalType,
    long Watermark) : DigitalBrain.Signal;
