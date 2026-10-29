using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.SystemOne;

public abstract class SystemOneModel : AiModel
{
    // Hosted precedes local, matching the other catalogs.
    public static IReadOnlyList<SystemOneModel> All { get; } =
    [
        new Models.TypeSafe.Jev(),
        new Models.Ollama.LocalJev(),
    ];

    public static SystemOneModel? FindByMarker(Type marker)
        => All.FirstOrDefault(model => model.Marker == marker);

    public static SystemOneModel? FindByMarkerName(string markerName)
        => All.FirstOrDefault(model => string.Equals(model.Marker.Name, markerName, StringComparison.Ordinal));
}

public abstract class SystemOneModel<TMarker> : SystemOneModel
    where TMarker : ISystemOneModel
{
    public sealed override Type Marker => typeof(TMarker);
}
