using IntoChat.OS.Contracts.Compute;
using MimeKit;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed partial class GmailNeuron
{
    private async Task<GmailInboxResult> ReadInboxCore(GmailInboxRequest request, CancellationToken ct)
    {
        RequireCaller();
        ct.ThrowIfCancellationRequested();
        if (GmailValidation.Validate(request) is { } invalid) { return GmailInboxResult.FromFailure(invalid); }
        await EnsureGrantBindingAsync();
        if (GrantFailure() is { } unavailable) { return GmailInboxResult.FromFailure(unavailable); }
        string? pageToken = null;
        if (request.PageToken is not null && !GmailPageTokens.TryRead(request.PageToken, FullKey,
            Snapshot.BoundMailbox!, Snapshot.GrantGeneration, Snapshot.PageTokenKey, out pageToken))
        { return GmailInboxResult.FromFailure(new("invalid_input", "The continuation does not belong to this mailbox grant.", [new("pageToken", "invalid", "Read a fresh inbox page.")])); }

        using var trace = EnterGoogleTrace();
        var context = NewCallContext();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), GmailClock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            return await UseCredentialAsync(async (credential, cancellation) =>
            {
                var listed = await Api.ListMessagesAsync(credential.Value, context, request.PageSize, pageToken, cancellation);
                if (listed.Failure is { } failure) { return GmailInboxResult.FromFailure(ProviderFailure(failure)); }
                var page = listed.Value!;
                var messages = new List<GmailMessageSummary>();
                var missing = new List<string>();
                // Four metadata requests at a time; list order is retained despite concurrent transport.
                foreach (var batch in page.Messages.Chunk(4))
                {
                    var hydrated = await Task.WhenAll(batch.Select(message => Api.ReadMetadataAsync(credential.Value, context, message.Id, cancellation)));
                    for (var index = 0; index < batch.Length; index++)
                    {
                        var metadata = hydrated[index];
                        if (metadata.Failure?.Kind == GoogleApiFailureKind.NotFound) { missing.Add(batch[index].Id); }
                        else if (metadata.Failure is { } error) { return GmailInboxResult.FromFailure(ProviderFailure(error)); }
                        else { messages.Add(Summary(metadata.Value!)); }
                    }
                }
                var next = page.NextPageToken is null ? null : GmailPageTokens.Write(page.NextPageToken, FullKey,
                    Snapshot.BoundMailbox!, Snapshot.GrantGeneration, Snapshot.PageTokenKey);
                return GmailInboxResult.FromValue(new([.. messages], next, [.. missing]));
            }, linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        { return GmailInboxResult.FromFailure(Failure("provider_unavailable")); }
        catch (BudgetExhaustedException) { return GmailInboxResult.FromFailure(Failure("budget_exhausted")); }
        catch (InvalidDataException) { return GmailInboxResult.FromFailure(Failure("provider_unavailable")); }
        catch (GmailCredentialException error) { return GmailInboxResult.FromFailure(Failure(error.Code)); }
    }

    private async Task<GmailMessageResult> ReadMessageCore(GmailMessageRequest request, CancellationToken ct)
    {
        RequireCaller();
        ct.ThrowIfCancellationRequested();
        if (GmailValidation.Validate(request) is { } invalid) { return GmailMessageResult.FromFailure(invalid); }
        await EnsureGrantBindingAsync();
        if (GrantFailure() is { } unavailable) { return GmailMessageResult.FromFailure(unavailable); }
        using var trace = EnterGoogleTrace();
        var context = NewCallContext();
        try
        {
            var result = await UseCredentialAsync((credential, cancellation) => Api.ReadMessageAsync(credential.Value, context, request.MessageId, cancellation), ct);
            if (result.Failure is { } failure) { return GmailMessageResult.FromFailure(ProviderFailure(failure)); }
            var message = result.Value!;
            string? Body(string mediaType)
            {
                var parts = message.BodyParts.Where(part => part.MimeType == mediaType).Select(part => part.Content).ToArray();
                return parts.Length == 0 ? null : string.Concat(parts);
            }
            return GmailMessageResult.FromValue(new(Summary(message.Metadata), Addresses(message.Metadata.To), Addresses(message.Metadata.Cc),
                Body("text/plain"), Body("text/html"), message.Truncated,
                [.. message.Omissions.Select(omission => new GmailBodyOmission(omission.PartId, omission.MediaType, omission.Reason))], message.HasAttachments));
        }
        catch (BudgetExhaustedException) { return GmailMessageResult.FromFailure(Failure("budget_exhausted")); }
        catch (GmailCredentialException error) { return GmailMessageResult.FromFailure(Failure(error.Code)); }
    }

    private static GmailMessageSummary Summary(GoogleMessageMetadata metadata)
        => new(metadata.Id, metadata.ThreadId, metadata.From, metadata.Subject, metadata.ReceivedAt,
            metadata.Snippet ?? "", metadata.LabelIds.Contains("UNREAD", StringComparer.Ordinal));

    private static string[] Addresses(string? header)
        => header is not null && InternetAddressList.TryParse(header, out var addresses)
            ? [.. addresses.Mailboxes.Select(mailbox => mailbox.ToString())] : [];
}
