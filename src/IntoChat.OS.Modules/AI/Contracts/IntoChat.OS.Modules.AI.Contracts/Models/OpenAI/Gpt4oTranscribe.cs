using IntoChat.OS.Modules.AI.Voice;

namespace IntoChat.OS.Modules.AI.Models.OpenAI;

public sealed class Gpt4oTranscribe : TranscriptionModel<IGpt4oTranscribe>
{
    public override string Id => "gpt-4o-transcribe";

    public override AiProvider Provider => AiProvider.OpenAI;

    public override TranscriptionFormats Formats =>
        TranscriptionFormats.Text | TranscriptionFormats.Json;
}

public interface IGpt4oTranscribe : ITranscription;
