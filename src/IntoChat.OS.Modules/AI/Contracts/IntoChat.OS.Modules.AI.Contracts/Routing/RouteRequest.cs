namespace IntoChat.OS.Modules.AI.Routing;

// One request posted through IRouter.Ask. The poster picks the id; the answer arrives as Routed with
// the same id and stays readable through IRouter.Pending.
public sealed record RouteRequest(
    Guid Id,
    string State,
    string[]? Allowed = null);

// The router's record of one asked request: Routing until it is decided, then Routed with its
// result (no result, and the reason in Error, when nothing fits) or Failed with the error.
public sealed record PendingRoute(
    Guid Id,
    string State,
    string Status,
    string[]? Allowed = null,
    RouteResult? Result = null,
    string? Error = null);
