using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Routing.Signals;

// The router's answer to one asked request: which contract and method, and how sure it was. Id is
// the RouteRequest's; Contract is the full type name. Error is set when no route could be decided.
[Signal("ai.routed")]
public sealed record Routed(
    Guid Id,
    string Contract,
    string Method,
    double Confidence,
    string? Error = null) : DigitalBrain.Signal;
