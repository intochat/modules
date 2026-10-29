namespace IntoChat.OS.Modules.AI.Models.OpenRouter;

internal sealed class OpenRouterProviderFactory : OpenAICompatibleProviderFactory
{
    public override AiProvider Provider => AiProvider.OpenRouter;
}
