using DigitalBrain.Capabilities.Compute;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed partial class GmailNeuron
{
    private const int MaximumDraftEntries = 100;
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReceiptLifetime = TimeSpan.FromHours(24);
    // If even the recovery write fails, keep the activation fail-closed; durable Pending also recovers as Unknown.
    private readonly Dictionary<string, GmailDraftOperation> _uncertainOutcomes = new(StringComparer.Ordinal);

    private async Task<GmailDraftPreviewResult> PreviewDraftCore(GmailDraftRequest request, CancellationToken ct)
    {
        var caller = CallerScope();
        ct.ThrowIfCancellationRequested();
        if (GmailValidation.Validate(request) is { } invalid) { return GmailDraftPreviewResult.FromFailure(invalid); }
        await EnsureGrantBindingAsync();
        if (GrantFailure() is { } unavailable) { return GmailDraftPreviewResult.FromFailure(unavailable); }
        var now = GmailClock.GetUtcNow();
        var retained = Snapshot.DraftPreviews.Where(preview => preview.Value.ExpiresAt > now).ToArray();
        if (retained.Length >= MaximumDraftEntries)
        { return GmailDraftPreviewResult.FromFailure(Failure("rate_limited", retained.Min(preview => preview.Value.ExpiresAt))); }
        var value = new GmailPreparedDraft(Guid.NewGuid().ToString("N"), [.. request.To], [.. request.Cc], [.. request.Bcc],
            request.Subject, request.Body, now + PreviewLifetime);
        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        var stored = new GmailStoredPreview(value, caller, Snapshot.BoundMailbox!, Snapshot.GrantGeneration, digest);
        ct.ThrowIfCancellationRequested();
        await SaveGmailStateAsync(next => next.DraftPreviews = [.. retained, stored]);
        return GmailDraftPreviewResult.FromValue(value);
    }

    private async Task<GmailDraftOperationResult> GetDraftOperationCore(GmailDraftOperationRequest request, CancellationToken ct)
    {
        var caller = CallerScope();
        ct.ThrowIfCancellationRequested();
        if (GmailValidation.Validate(request) is { } invalid) { return GmailDraftOperationResult.FromFailure(invalid); }
        await EnsureGrantBindingAsync();
        var operation = FindOperation(request.RequestId, GmailClock.GetUtcNow());
        return operation is not null && operation.CallerScope == caller
            ? GmailDraftOperationResult.FromValue(CurrentOutcome(operation))
            : GmailDraftOperationResult.FromFailure(Failure("not_found"));
    }

    private async Task<GmailDraftCreationResult> CreateDraftCore(GmailCreateDraftRequest request, CancellationToken ct)
    {
        var caller = CallerScope();
        ct.ThrowIfCancellationRequested();
        if (GmailValidation.Validate(request) is { } invalid) { return GmailDraftCreationResult.FromFailure(invalid); }
        await EnsureGrantBindingAsync();
        var now = GmailClock.GetUtcNow();
        // Receipts are authoritative before today's grant, mailbox or preview expiry is considered.
        if (FindOperation(request.RequestId, now) is { } existing)
        {
            if (existing.CallerScope != caller) { return GmailDraftCreationResult.FromFailure(Failure("not_found")); }
            return existing.Value.PreviewId == request.PreviewId
                ? GmailDraftCreationResult.FromValue(CurrentOutcome(existing))
                : GmailDraftCreationResult.FromFailure(Failure("request_conflict"));
        }
        var preview = Snapshot.DraftPreviews.FirstOrDefault(candidate => candidate.Value.PreviewId == request.PreviewId);
        if (preview is null || preview.CallerScope != caller) { return GmailDraftCreationResult.FromFailure(Failure("not_found")); }
        if (preview.RequestId is not null) { return GmailDraftCreationResult.FromFailure(Failure("request_conflict")); }
        if (preview.Value.ExpiresAt <= now) { return GmailDraftCreationResult.FromFailure(Failure("preview_expired")); }
        if (GrantFailure(compose: true) is { } unavailable) { return GmailDraftCreationResult.FromFailure(unavailable); }
        if (preview.GrantGeneration != Snapshot.GrantGeneration || preview.Mailbox != Snapshot.BoundMailbox)
        { return GmailDraftCreationResult.FromFailure(Failure("request_conflict")); }
        var retained = RetainedOperations(now);
        if (retained.Length >= MaximumDraftEntries)
        {
            var retryAt = retained.Min(operation => operation.Value.RetainUntil);
            return GmailDraftCreationResult.FromFailure(Failure("rate_limited", retryAt));
        }
        var prepared = preview.Value;
        var mime = await GmailDraftPayload.EncodeAsync(prepared.To, prepared.Cc, prepared.Bcc, prepared.Subject, prepared.Body, ct);
        using var trace = EnterGoogleTrace();
        var context = NewCallContext();
        var accounting = new GmailDraftAccounting(TraceContext.Current!.TraceId, context.InvocationId, GmailAccountingStatus.Pending);
        try
        {
            return await UseCredentialAsync(async (credential, cancellation) =>
            {
                // A refresh may narrow scopes; check again before durable intent or any mutation.
                if (GrantFailure(compose: true) is { } changedGrant) { return GmailDraftCreationResult.FromFailure(changedGrant); }
                cancellation.ThrowIfCancellationRequested();
                var decisions = await AllowRecipientsAsync(prepared, cancellation);
                var pending = new GmailDraftOperation(request.RequestId, prepared.PreviewId, preview.Mailbox,
                    GmailDraftOperationStatus.Pending, null, null, now, now, null, accounting);
                var receipt = new GmailStoredOperation(pending, caller, preview.GrantGeneration, preview.Digest);
                try
                {
                    await SaveGmailStateAsync(next =>
                    {
                        next.DraftPreviews = [.. next.DraftPreviews.Select(candidate => candidate.Value.PreviewId == prepared.PreviewId
                            ? candidate with { RequestId = request.RequestId } : candidate)];
                        next.DraftOperations = [.. retained, receipt];
                    });
                }
                catch
                {
                    await ReleaseAsync(decisions);
                    throw;
                }
                if (cancellation.IsCancellationRequested)
                {
                    await ReleaseAsync(decisions);
                    await PersistDraftOutcome(receipt, FinalOutcome(pending, GmailDraftOperationStatus.Failed,
                        Failure("cancelled_before_dispatch"), accounting with { Status = GmailAccountingStatus.CancelledBeforeDispatch }));
                    cancellation.ThrowIfCancellationRequested();
                }
                GoogleDraftWriteResult result;
                try { result = await Api.CreateDraftAsync(credential.Value, context, mime, cancellation); }
                catch (BudgetExhaustedException)
                {
                    await ReleaseAsync(decisions);
                    var denied = await PersistDraftOutcome(receipt, FinalOutcome(pending, GmailDraftOperationStatus.Failed,
                        Failure("budget_exhausted"), accounting with { Status = GmailAccountingStatus.CancelledBeforeDispatch }));
                    return GmailDraftCreationResult.FromValue(denied);
                }
                catch
                {
                    // An unexpected adapter failure after invocation is not evidence of no dispatch: the draft may exist, so its
                    // policy reservations stay counted. Keep recovery safe, then surface the bug/cancellation.
                    await PersistDraftOutcome(receipt, FinalOutcome(pending, GmailDraftOperationStatus.Unknown, Failure("outcome_unknown"), accounting));
                    throw;
                }
                // Only a definite non-dispatch gives the reservations back; an unknown outcome stays counted.
                if (result.Outcome is GoogleWriteOutcome.Rejected or GoogleWriteOutcome.NotDispatched)
                {
                    await ReleaseAsync(decisions);
                }
                var accounted = accounting with
                {
                    Status = result.Accounting?.Status switch
                    {
                        ExternalAccountingStatus.Settled => GmailAccountingStatus.Settled,
                        ExternalAccountingStatus.CancelledBeforeDispatch => GmailAccountingStatus.CancelledBeforeDispatch,
                        _ => GmailAccountingStatus.Pending,
                    }
                };
                var completed = result.Outcome switch
                {
                    GoogleWriteOutcome.Succeeded => FinalOutcome(pending, GmailDraftOperationStatus.Completed, null, accounted, result.DraftId),
                    GoogleWriteOutcome.Rejected => FinalOutcome(pending, GmailDraftOperationStatus.Failed, ProviderFailure(result.Failure!), accounted),
                    GoogleWriteOutcome.NotDispatched => FinalOutcome(pending, GmailDraftOperationStatus.Failed,
                        result.CancellationRequested ? Failure("cancelled_before_dispatch") : result.Failure is { } failure ? ProviderFailure(failure) : Failure("provider_unavailable"),
                        accounted with { Status = GmailAccountingStatus.CancelledBeforeDispatch }),
                    _ => FinalOutcome(pending, GmailDraftOperationStatus.Unknown, Failure("outcome_unknown"), accounted),
                };
                var saved = await PersistDraftOutcome(receipt, completed);
                if (result.CancellationRequested && result.Outcome != GoogleWriteOutcome.Succeeded)
                { throw new OperationCanceledException(cancellation); }
                return GmailDraftCreationResult.FromValue(saved);
            }, ct);
        }
        catch (GmailCredentialException error) { return GmailDraftCreationResult.FromFailure(Failure(error.Code)); }
        catch (BudgetExhaustedException) { return GmailDraftCreationResult.FromFailure(Failure("budget_exhausted")); }
    }

    // One policy decision per distinct recipient across To, Cc and Bcc, so a recipient rule sees every address rather than a
    // joined string. A refusal for any address releases the others and refuses the draft; a draft counts once per recipient.
    private async Task<IReadOnlyList<string>> AllowRecipientsAsync(GmailPreparedDraft prepared, CancellationToken ct)
    {
        var decisions = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, addresses) in new (string, IEnumerable<string>)[] { ("To", prepared.To), ("Cc", prepared.Cc), ("Bcc", prepared.Bcc) })
        {
            foreach (var address in addresses)
            {
                if (!seen.Add(address)) { continue; }
                var decision = await Policies.Allow("gmail.draft.create", 1m, new Dictionary<string, string>
                {
                    ["To"] = address,
                    ["Field"] = field,
                    ["Subject"] = prepared.Subject,
                }, ct);
                if (!decision.Allowed)
                {
                    await ReleaseAsync(decisions);
                    DigitalBrain.PolicyGuard.Require(decision);
                }
                decisions.Add(decision.DecisionId);
            }
        }
        return decisions;
    }

    private async Task ReleaseAsync(IReadOnlyList<string> decisions)
    {
        foreach (var decision in decisions) { await Policies.Release(decision, CancellationToken.None); }
    }

    private GmailStoredOperation[] RetainedOperations(DateTimeOffset now)
        => [.. Snapshot.DraftOperations.Where(operation => operation.Value.RetainUntil is null || operation.Value.RetainUntil > now)];

    private GmailStoredOperation? FindOperation(string requestId, DateTimeOffset now)
        => Snapshot.DraftOperations.FirstOrDefault(operation => operation.Value.RequestId == requestId
            && (operation.Value.RetainUntil is null || operation.Value.RetainUntil > now));

    private GmailDraftOperation CurrentOutcome(GmailStoredOperation operation)
        => _uncertainOutcomes.GetValueOrDefault(operation.Value.RequestId) ?? operation.Value;

    private GmailDraftOperation FinalOutcome(GmailDraftOperation pending, GmailDraftOperationStatus status,
        GmailFailure? failure, GmailDraftAccounting accounting, string? draftId = null)
    {
        var now = GmailClock.GetUtcNow();
        return pending with
        {
            Status = status,
            Failure = failure,
            DraftId = draftId,
            Accounting = accounting,
            UpdatedAt = now,
            RetainUntil = status is GmailDraftOperationStatus.Completed or GmailDraftOperationStatus.Failed ? now + ReceiptLifetime : null
        };
    }

    private async Task<GmailDraftOperation> PersistDraftOutcome(GmailStoredOperation receipt, GmailDraftOperation outcome)
    {
        Task Persist(GmailDraftOperation value) => SaveGmailStateAsync(next => next.DraftOperations = [.. next.DraftOperations.Select(operation =>
            operation.Value.RequestId == receipt.Value.RequestId ? operation with { Value = value } : operation)]);
        try { await Persist(outcome); return outcome; }
        catch (Exception error)
        {
            var uncertain = outcome with { Status = GmailDraftOperationStatus.Unknown, Failure = Failure("outcome_unknown"), RetainUntil = null };
            _uncertainOutcomes[receipt.Value.RequestId] = uncertain;
            ServiceProvider.GetRequiredService<ILogger<GmailNeuron>>().LogWarning("Gmail draft receipt persistence failed ({ErrorType}); retry protection remains pending.", error.GetType().Name);
            try { await Persist(uncertain); }
            catch (Exception recoveryError)
            {
                ServiceProvider.GetRequiredService<ILogger<GmailNeuron>>().LogWarning("Gmail draft recovery persistence failed ({ErrorType}); the pending receipt remains protected.", recoveryError.GetType().Name);
            }
            return uncertain;
        }
    }
}
