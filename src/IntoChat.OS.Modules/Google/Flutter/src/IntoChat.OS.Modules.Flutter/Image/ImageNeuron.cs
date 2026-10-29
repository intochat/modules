using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Image.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Image;

[Neuron(UIVocabulary.ImageType)]
internal sealed class ImageNeuron
    : UiNeuron<ImageState>, IImage
{
    public Task Set(string url, string mediaType, string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(mediaType);
        ArgumentNullException.ThrowIfNull(prompt);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Url = url.Trim();
        next.MediaType = mediaType.Trim();
        next.Prompt = prompt;
        return Save(next, new ImageChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ImageState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
