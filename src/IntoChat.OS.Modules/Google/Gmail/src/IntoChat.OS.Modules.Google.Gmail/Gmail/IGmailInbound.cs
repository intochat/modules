namespace IntoChat.OS.Modules.Google.Gmail;

[DigitalBrain.Contracts.NeuronAlias("gmail.inbound"), DigitalBrain.Contracts.DefaultNeuronType("gmail")]
internal interface IGmailInbound
{
    Task<GmailPushAcceptance> AcceptWatchPush(GmailWatchPush push);
}

internal enum GmailPushAcceptance { Accepted, Duplicate, Ignored, Invalid, Retryable }
