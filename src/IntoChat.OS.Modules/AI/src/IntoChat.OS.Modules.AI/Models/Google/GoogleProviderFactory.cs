namespace IntoChat.OS.Modules.AI.Models.Google;

internal sealed class GoogleProviderFactory : OpenAICompatibleProviderFactory
{
    public override AiProvider Provider => AiProvider.Google;
}
