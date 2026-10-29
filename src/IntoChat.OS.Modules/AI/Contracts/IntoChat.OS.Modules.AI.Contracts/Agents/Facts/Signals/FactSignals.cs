using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Agents.Facts.Signals;

[Signal("facts.asserted")]
public sealed record FactAsserted(string FactsId, Guid FactId, string DedupKey) : DigitalBrain.Signal;

[Signal("facts.invalidated")]
public sealed record FactInvalidated(string FactsId, Guid FactId, Guid? SupersededBy) : DigitalBrain.Signal;

[Signal("facts.forgotten")]
public sealed record FactForgotten(string FactsId, Guid FactId) : DigitalBrain.Signal;
