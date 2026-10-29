namespace IntoChat.OS.Modules.Google.Gmail;

[GenerateSerializer, Alias("gmail.credential-failure")]
internal sealed class GmailCredentialException(string code) : InvalidOperationException("The Gmail credential could not be refreshed.")
{
    [Id(0)] public string Code { get; } = code;
}
