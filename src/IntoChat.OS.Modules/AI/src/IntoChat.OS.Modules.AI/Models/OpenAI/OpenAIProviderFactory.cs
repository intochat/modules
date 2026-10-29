namespace IntoChat.OS.Modules.AI.Models.OpenAI;

internal sealed class OpenAIProviderFactory : OpenAICompatibleProviderFactory
{
    public override AiProvider Provider => AiProvider.OpenAI;
}
