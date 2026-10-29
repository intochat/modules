using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Video.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Video;

[Neuron(UIVocabulary.VideoType)]
internal sealed class VideoNeuron
    : UiNeuron<VideoState>, IVideo
{
    public Task Load(string url, double duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (duration < 0) { throw new ArgumentOutOfRangeException(nameof(duration)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Url = url.Trim();
        next.Duration = duration;
        next.Position = 0;
        next.Playing = false;
        return Save(next, new VideoChanged(FullKey, false));
    }

    public Task Play()
    {
        var next = RequireLoaded();
        next.Version++;
        next.Playing = true;
        return Save(next, new VideoChanged(FullKey, true));
    }

    public Task Pause()
    {
        var next = RequireLoaded();
        next.Version++;
        next.Playing = false;
        return Save(next, new VideoChanged(FullKey, false));
    }

    public Task Seek(double seconds)
    {
        var next = RequireLoaded();
        next.Version++;
        next.Position = Math.Clamp(seconds, 0, next.Duration);
        return Save(next, new VideoChanged(FullKey, next.Playing));
    }

    public Task End()
    {
        var next = RequireLoaded();
        next.Version++;
        next.Playing = false;
        next.Position = next.Duration;
        return Save(next, new VideoChanged(FullKey, false), new VideoEnded(FullKey));
    }

    [DigitalBrain.ReadOnlyCall] public Task<VideoState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }

    private VideoState RequireLoaded()
    {
        var next = Snapshot;
        next.Name = FullKey;
        if (string.IsNullOrWhiteSpace(next.Url)) { throw new InvalidOperationException("Video has no source."); }
        return next;
    }
}
