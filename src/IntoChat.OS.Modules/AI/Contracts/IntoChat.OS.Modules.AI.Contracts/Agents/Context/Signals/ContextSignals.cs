using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Agents.Context.Signals;

[Signal("agent-context.updated")]
public sealed record ContextUpdated(string ContextId, long Revision, string Name) : DigitalBrain.Signal;

[Signal("agent-context.removed")]
public sealed record ContextRemoved(string ContextId, long Revision, string Name) : DigitalBrain.Signal;
