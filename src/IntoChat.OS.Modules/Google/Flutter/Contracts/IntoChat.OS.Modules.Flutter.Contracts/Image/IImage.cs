using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Image;

[NeuronAlias("image"), DefaultNeuronType(UIVocabulary.ImageType)]
public interface IImage
{
    Task Set(string url, string mediaType, string prompt);
    [DigitalBrain.ReadOnlyCall] Task<ImageState> Read();
}

[ExportableState]
public sealed class ImageState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Prompt { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string Url { get; set; } = "";
}
