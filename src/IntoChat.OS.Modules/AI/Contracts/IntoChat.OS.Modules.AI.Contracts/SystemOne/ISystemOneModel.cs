using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.SystemOne;

// Marker root for System One (decision) model markers. One root for the hosted TypeSafe endpoint
// and the local adapter: which one serves a marker is a matter of AiProvider, not of catalog.
public interface ISystemOneModel : IAiMarker;
