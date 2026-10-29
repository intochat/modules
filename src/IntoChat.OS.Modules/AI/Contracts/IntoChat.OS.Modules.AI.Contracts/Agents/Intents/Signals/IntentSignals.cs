using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Agents.Intents.Signals;

[Signal("intents.heard")]
public sealed record IntentHeard(string IntentsId, Guid IntentId, string RequestId) : DigitalBrain.Signal;

[Signal("intents.rephrased")]
public sealed record IntentRephrased(string IntentsId, Guid IntentId, string RequestId, double Confidence) : DigitalBrain.Signal;

[Signal("intents.transitioned")]
public sealed record IntentTransitioned(string IntentsId, Guid IntentId, IntentState From, IntentState To) : DigitalBrain.Signal;

[Signal("intents.armed")]
public sealed record IntentArmed(string IntentsId, Guid IntentId) : DigitalBrain.Signal;

[Signal("intents.cue-fired")]
public sealed record IntentCueFired(string IntentsId, Guid IntentId, string Cue) : DigitalBrain.Signal;
