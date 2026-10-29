using IntoChat.OS.Modules.AI.Voice;

namespace IntoChat.OS.Modules.AI.Models.FoundryLocal;

public sealed class WhisperTiny : TranscriptionModel<IWhisperTiny>
{
    public override string Id => "whisper-tiny";

    public override AiProvider Provider => AiProvider.FoundryLocal;
}

public interface IWhisperTiny : ITranscription;
