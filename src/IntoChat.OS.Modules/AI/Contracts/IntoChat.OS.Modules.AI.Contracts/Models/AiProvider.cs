namespace IntoChat.OS.Modules.AI.Models;

// Who serves a model. Spans every model kind, not just chat, so a locally
// hosted transcription model is describable in the same vocabulary as a cloud
// chat model.
public enum AiProvider
{
    OpenAI,
    Anthropic,
    Google,
    XAI,
    Ollama,
    FoundryLocal,
    OpenRouter,
    // TypeSafe's hosted System One (Jev). Decisions only; never a chat provider.
    TypeSafe,
    // The deployment's registered IChatClient, used when nothing names a model and no provider is configured.
    Host,
}
