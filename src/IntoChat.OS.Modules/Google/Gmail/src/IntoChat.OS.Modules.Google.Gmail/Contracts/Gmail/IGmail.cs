using IntoChat.OS.Modules.Tools;

namespace IntoChat.OS.Modules.Google.Gmail;

[DigitalBrain.Contracts.NeuronAlias("gmail"), DigitalBrain.Contracts.DefaultNeuronType("gmail")]
public interface IGmail : IToolSource
{
    Task<GmailInboxResult> ReadInbox(GmailInboxRequest request, CancellationToken ct = default);
    Task<GmailMessageResult> ReadMessage(GmailMessageRequest request, CancellationToken ct = default);
    Task<GmailDraftPreviewResult> PreviewDraft(GmailDraftRequest request, CancellationToken ct = default);
    Task<GmailDraftCreationResult> CreateDraft(GmailCreateDraftRequest request, CancellationToken ct = default);
    Task<GmailDraftOperationResult> GetDraftOperation(GmailDraftOperationRequest request, CancellationToken ct = default);
}

[GenerateSerializer, Alias("gmail.watch-push")]
public sealed record GmailWatchPush(
    [property: Id(0)] string HistoryId,
    [property: Id(1)] string EmailAddress);
