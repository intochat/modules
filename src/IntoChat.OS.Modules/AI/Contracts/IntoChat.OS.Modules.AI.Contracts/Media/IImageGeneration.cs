namespace IntoChat.OS.Modules.AI.Media;

public sealed record GeneratedUiImage(byte[] Content, string MediaType, string Model);

public interface IImageGeneration
{
    Task<GeneratedUiImage> GenerateAsync(string prompt, CancellationToken cancellationToken);
}
