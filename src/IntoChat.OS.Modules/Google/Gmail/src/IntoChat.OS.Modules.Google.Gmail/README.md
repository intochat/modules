# IntoChat.OS.Modules.Google.Gmail

Gmail reads, local draft previews, recoverable draft creation, browser sign-in, and mailbox wake-ups. Sign-in goes through the `google-auth` module; Google Drive is the separate `google-drive` module. Draft creation never sends mail.

Module id: `gmail`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Google/Gmail/tests/IntoChat.OS.Modules.Google.Gmail.Tests.Integration/IntoChat.OS.Modules.Google.Gmail.Tests.Integration.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

Scripts select a full connection key through configuration or kernel discovery. Reads return one bounded provider page; continuation tokens are mailbox-grant scoped and provider ordering is best effort. Inspect `Failure` before `Value`.

```csharp
using IntoChat.OS.Modules.Google.Gmail;
await using var brain = await IntoChatOSClient.ConnectAsync(args);
var key = brain.Setting("GmailConnection") ?? throw new InvalidOperationException("Select a Gmail connection.");
var gmail = brain.Get<IGmail>(key);
var result = await gmail.ReadInbox(new GmailInboxRequest(PageSize: 25), brain.Stopping);
if (result.Failure is { } failure) { Console.WriteLine(failure.Code); return; }
foreach (var message in result.Value!.Messages)
    Console.WriteLine(message.MessageId + ": " + message.Subject);
// Pass result.Value.NextPageToken in a subsequent bounded ReadInbox request.
```

`MailReceived` means reconcile the mailbox. Subscribe before the initial read; the callback below runs after subscription. Watch delivery requires configured Pub/Sub. Duplicate wake-ups are possible.

```csharp
using IntoChat.OS.Modules.Google.Gmail;
await using var brain = await IntoChatOSClient.ConnectAsync(args);
var gmail = brain.Get<IGmail>(brain.Setting("GmailConnection") ?? throw new InvalidOperationException("Select a Gmail connection."));
async Task Reconcile()
{
    var result = await gmail.ReadInbox(new GmailInboxRequest(), brain.Stopping);
    Console.WriteLine(result.Failure?.Code ?? $"Read {result.Value!.Messages.Length} messages.");
}
await foreach (var wakeup in brain.On<MailReceived>(gmail, Reconcile, brain.Stopping))
    await Reconcile();
```

`PreviewDraft` validates and stores a local preview for ten minutes without calling Google. Review its returned recipients, subject and body, then retain its preview ID and one request ID before calling `CreateDraft`. The example consumes those retained workflow settings. Reuse the same IDs after interruption; use `GetDraftOperation` for local recovery. Unknown outcomes require review and must not trigger a new automatic creation. Receipts can expire, so `not_found` does not prove absence of a provider draft. Compose requires separate consent for the same mailbox.

```csharp
using IntoChat.OS.Modules.Google.Gmail;
await using var brain = await IntoChatOSClient.ConnectAsync(args);
var gmail = brain.Get<IGmail>(brain.Setting("GmailConnection") ?? throw new InvalidOperationException("Select a Gmail connection."));
var previewId = brain.Setting("ReviewedPreviewId") ?? throw new InvalidOperationException("Review and retain a preview first.");
var requestId = brain.Setting("DraftRequestId") ?? throw new InvalidOperationException("Retain one request ID before dispatch.");
var result = await gmail.CreateDraft(new GmailCreateDraftRequest(previewId, requestId), brain.Stopping);
if (result.Failure is { } failure) { Console.WriteLine(failure.Code); return; }
var operation = result.Value!;
Console.WriteLine($"{operation.Status}: {operation.DraftId}");
// Pending/Unknown: query GetDraftOperation(new GmailDraftOperationRequest(requestId)).
// Accounting may remain Pending even when the provider draft is Completed.
```

The four tools use the same requests, validation and results. Call the connection's `Tools()` for complete schemas. ReadMessage exposes separate text/HTML with explicit body omissions and a 256 KiB aggregate limit; it does not download attachments.

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:gmail:Options` (environment prefix `IntoChatOS__Modules__gmail__Options__`).

Options:

| Option | Type | Default | Required |
| --- | --- | --- | --- |
| `Audience` | integration setting | unset | yes |
| `ServiceAccountEmail` | integration setting | unset | yes |
| `TopicName` | integration setting | unset | yes |

Secrets (names only; values are sealed per brain, never configuration):

None.

Contracts: assembly `IntoChat.OS.Modules.Google.Gmail` (`IGmail`).
Hosting: runs in process.
<!-- END GENERATED -->
