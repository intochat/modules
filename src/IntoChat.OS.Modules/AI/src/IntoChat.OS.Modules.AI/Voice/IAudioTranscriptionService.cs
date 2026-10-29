namespace IntoChat.OS.Modules.AI;

public interface IAudioTranscriptionService
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
    bool InitializationFailed { get; }
    string? ErrorMessage { get; }
    string ModelId { get; }

    Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default);

    Task<string> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
