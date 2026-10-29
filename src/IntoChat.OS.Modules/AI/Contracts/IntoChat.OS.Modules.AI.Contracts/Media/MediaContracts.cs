using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Media;

public enum MediaOperation { ImageGeneration, Embedding, SpeechRecognition, SpeechSynthesis }

public sealed record MediaCapabilities(
    MediaOperation Operation,
    bool Available,
    int MaxPayloadBytes,
    string? UnavailableReason = null,
    int MaxTextCharacters = 32_768,
    int MaxBatchItems = 1);

// Bounded inline media for stage one; no remote URL fetching or filesystem access.
public sealed record MediaPayload(byte[] Content, string MediaType);
public sealed record ImageGenerationRequest(string Prompt);
public sealed record ImageGenerationResult(Guid OperationId, MediaPayload Image, string Model);
public sealed record EmbeddingRequest(string[] Inputs);
public sealed record EmbeddingResult(Guid OperationId, float[][] Vectors);
public sealed record SpeechRecognitionRequest(MediaPayload Audio, string FileName);
public sealed record SpeechRecognitionResult(Guid OperationId, string Text, string Model);
public sealed record SpeechSynthesisRequest(string Text, string Voice);
public sealed record SpeechSynthesisResult(Guid OperationId, MediaPayload Audio, string Model);

[NeuronAlias("ai.media.IImageGenerator"), DefaultNeuronType("imagegeneratorneuron")]
public interface IImageGenerator
{
    [DigitalBrain.ReadOnlyCall] Task<MediaCapabilities> Describe();
    [DigitalBrain.CallTimeout(600000)] Task<ImageGenerationResult> Generate(ImageGenerationRequest request, CancellationToken cancellationToken = default);
}
[NeuronAlias("ai.media.IEmbeddingModel"), DefaultNeuronType("embeddingneuron")]
public interface IEmbeddingModel
{
    [DigitalBrain.ReadOnlyCall] Task<MediaCapabilities> Describe();
    [DigitalBrain.CallTimeout(600000)] Task<EmbeddingResult> Embed(EmbeddingRequest request, CancellationToken cancellationToken = default);
}
[NeuronAlias("ai.media.ISpeechRecognizer"), DefaultNeuronType("speechrecognizerneuron")]
public interface ISpeechRecognizer
{
    [DigitalBrain.ReadOnlyCall] Task<MediaCapabilities> Describe();
    [DigitalBrain.CallTimeout(600000)] Task<SpeechRecognitionResult> Recognize(SpeechRecognitionRequest request, CancellationToken cancellationToken = default);
}
[NeuronAlias("ai.media.ISpeechSynthesizer"), DefaultNeuronType("speechsynthesizerneuron")]
public interface ISpeechSynthesizer
{
    [DigitalBrain.ReadOnlyCall] Task<MediaCapabilities> Describe();
    [DigitalBrain.CallTimeout(600000)] Task<SpeechSynthesisResult> Synthesize(SpeechSynthesisRequest request, CancellationToken cancellationToken = default);
}

[Signal("ai.media.ImageGenerated")]
public sealed record ImageGenerated(Guid OperationId, string Model) : DigitalBrain.Signal;
[Signal("ai.media.EmbeddingsGenerated")]
public sealed record EmbeddingsGenerated(Guid OperationId, int Count) : DigitalBrain.Signal;
[Signal("ai.media.SpeechRecognized")]
public sealed record SpeechRecognized(Guid OperationId, string Model) : DigitalBrain.Signal;
[Signal("ai.media.SpeechSynthesized")]
public sealed record SpeechSynthesized(Guid OperationId, string Model) : DigitalBrain.Signal;
[Signal("ai.media.MediaOperationFailed")]
public sealed record MediaOperationFailed(Guid OperationId, MediaOperation Operation,
    string Code) : DigitalBrain.Signal;
