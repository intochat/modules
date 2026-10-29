using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Video;

[NeuronAlias("video"), DefaultNeuronType(UIVocabulary.VideoType)]
public interface IVideo
{
    Task Load(string url, double duration);
    Task Play();
    Task Pause();
    Task Seek(double seconds);
    Task End();
    [DigitalBrain.ReadOnlyCall] Task<VideoState> Read();
}

[ExportableState]
public sealed class VideoState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Url { get; set; } = "";
    public bool Playing { get; set; }
    public double Position { get; set; }
    public double Duration { get; set; }
}
