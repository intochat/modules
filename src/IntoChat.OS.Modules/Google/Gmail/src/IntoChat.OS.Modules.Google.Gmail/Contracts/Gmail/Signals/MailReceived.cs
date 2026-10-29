namespace IntoChat.OS.Modules.Google.Gmail;

// A mailbox-change wake-up: reconcile current state; this does not identify a fetched message.
[DigitalBrain.Signal("gmail.mail-received")]
public sealed record MailReceived(
    string EmailAddress,
    string HistoryId) : DigitalBrain.Signal;
