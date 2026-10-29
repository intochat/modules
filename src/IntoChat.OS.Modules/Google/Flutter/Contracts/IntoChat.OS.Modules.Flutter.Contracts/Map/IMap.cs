using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Map;

[NeuronAlias("map"), DefaultNeuronType(UIVocabulary.MapType)]
public interface IMap
{
    Task Set(double lat, double lng, double zoom, IReadOnlyList<MapMarker> markers);
    [DigitalBrain.ReadOnlyCall] Task<MapState> Read();
}

public sealed record MapMarker(
    string Id,
    double Lat,
    double Lng,
    string Label);

[ExportableState]
public sealed class MapState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Zoom { get; set; } = 1;
    public List<MapMarker> Markers { get; set; } = [];
}
