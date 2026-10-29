using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Map.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Map;

[Neuron(UIVocabulary.MapType)]
internal sealed class MapNeuron
    : UiNeuron<MapState>, IMap
{
    public Task Set(double lat, double lng, double zoom, IReadOnlyList<MapMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        if (zoom <= 0) { throw new ArgumentOutOfRangeException(nameof(zoom)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Lat = lat;
        next.Lng = lng;
        next.Zoom = zoom;
        next.Markers = [.. markers];
        return Save(next, new MapChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<MapState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
