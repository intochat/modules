using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;

// A synapse finished with what was delivered up to Watermark: Batches batches acknowledged together
// (Succeeded), an exit while they were in flight (Failed, with Error), or Batches signals dropped past the
// buffer cap (Dropped, no duration), or batches the synapse passed over unhandled (Skipped: another operation
// of its app; not counted as a run). Published by the brain's runs neuron under the batch's trace.
[DigitalBrain.Signal("microsoft.csharp.behavior-ran")]
public sealed record BehaviorRan(
    string RuntimeId, string Owner, string Behavior,
    string Hash, string Source, string SignalType,
    long Watermark, RunOutcome Outcome, int Batches,
    long DurationMicroseconds, long CpuMicroseconds,
    string? Error) : DigitalBrain.Signal;
