using DigitalBrain.Capabilities;
using IntoChat.OS.Modules.Google.Gmail;
using System.Security.Cryptography;
using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Compute;
using IntoChat.OS.Connections.Platform.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Google.Gmail;

[DigitalBrain.Neuron("gmail", State = "gmail")]
internal sealed partial class GmailNeuron()
    : ConnectionNeuron<GmailState>, IGmail, IGmailInbound
{
    private sealed record ToolAvailability(bool AcceptedMailbox, bool DraftReceipts);
    private volatile ToolAvailability _toolAvailability = new(false, false);
    private bool _grantAcceptancePending;
    private bool _acceptingGrant;
    private bool _grantWriteCommitted;
    private bool _recoveredDrafts;

    private IGmailApi Api => ServiceProvider.GetRequiredService<IGmailApi>();
    private TimeProvider GmailClock => ServiceProvider.GetKeyedService<TimeProvider>(GmailModule.ClockKey)
        ?? ServiceProvider.GetRequiredService<TimeProvider>();
    private bool HasAcceptedMailbox => _toolAvailability.AcceptedMailbox;
    private bool HasDraftReceipts => _toolAvailability.DraftReceipts;
    private bool IsGrantReady => !_grantAcceptancePending && Snapshot.GrantGeneration > 0
        && Snapshot.Status != ConnectionStatus.Disconnected && Snapshot.Secrets.Length > 0
        && Snapshot.Account is not null && string.Equals(Snapshot.Account, Snapshot.BoundMailbox, StringComparison.OrdinalIgnoreCase);

    public Task<GmailInboxResult> ReadInbox(GmailInboxRequest request, CancellationToken ct = default) => ReadInboxCore(request, ct);
    public Task<GmailMessageResult> ReadMessage(GmailMessageRequest request, CancellationToken ct = default) => ReadMessageCore(request, ct);
    public Task<GmailDraftPreviewResult> PreviewDraft(GmailDraftRequest request, CancellationToken ct = default) => PreviewDraftCore(request, ct);
    public Task<GmailDraftCreationResult> CreateDraft(GmailCreateDraftRequest request, CancellationToken ct = default) => CreateDraftCore(request, ct);
    public Task<GmailDraftOperationResult> GetDraftOperation(GmailDraftOperationRequest request, CancellationToken ct = default) => GetDraftOperationCore(request, ct);

    internal Task SaveGmailStateAsync(Action<GmailState> change)
    {
        var next = Snapshot.Copy();
        change(next);
        return SaveQuietly(next);
    }

    public override async Task<ConnectionView> AcceptGrant(OAuthGrant grant)
    {
        var before = Snapshot.Copy();
        _acceptingGrant = true;
        _grantWriteCommitted = false;
        try { return await base.AcceptGrant(grant); }
        catch
        {
            if (!_grantWriteCommitted)
            {
                Restore(before);
                // A failed acknowledgement may hide a successful write: the grant stays pending until a save commits.
                _grantAcceptancePending = true;
                PublishToolAvailability();
            }
            throw;
        }
        finally { _acceptingGrant = false; }
    }

    protected override void OnCommitted()
    {
        if (_acceptingGrant)
        {
            _grantWriteCommitted = true;
            _grantAcceptancePending = false;
        }
        PublishToolAvailability();
    }
    private void PublishToolAvailability() => _toolAvailability = new(IsGrantReady, Snapshot.DraftOperations.Length > 0);

    private void PrepareGrantBinding(string mailbox, string authMethodId)
    {
        if (authMethodId == "google-oauth-compose" && Snapshot.BoundMailbox is { } previous
            && !string.Equals(previous, mailbox, StringComparison.OrdinalIgnoreCase))
        { throw new GmailUnavailableException("Compose consent must use the mailbox already selected by this connection."); }
        // Base AcceptGrant persists these fields together with the sealed grant. Until it completes, fail closed.
        _grantAcceptancePending = true;
        Snapshot.GrantGeneration = checked(Snapshot.GrantGeneration + 1);
        Snapshot.BoundMailbox = mailbox;
        Snapshot.Account = mailbox;
        Snapshot.PageTokenKey = RandomNumberGenerator.GetBytes(32);
        Snapshot.DraftPreviews = [];
        PublishToolAvailability();
    }

    private void CompleteGrantBinding()
    {
        _grantAcceptancePending = false;
        PublishToolAvailability();
    }

    private async Task EnsureGrantBindingAsync()
    {
        if (_grantAcceptancePending) { return; }
        if (Snapshot.Status != ConnectionStatus.Disconnected && Snapshot.Secrets.Length > 0 && Snapshot.Account is { } mailbox
            && (Snapshot.GrantGeneration == 0 || Snapshot.PageTokenKey.Length != 32
                || !string.Equals(Snapshot.BoundMailbox, mailbox, StringComparison.OrdinalIgnoreCase)))
        {
            await SaveGmailStateAsync(next =>
            {
                next.GrantGeneration = checked(next.GrantGeneration + 1);
                next.BoundMailbox = mailbox;
                next.PageTokenKey = RandomNumberGenerator.GetBytes(32);
                next.DraftPreviews = [];
            });
        }
        if (!_recoveredDrafts)
        {
            var now = GmailClock.GetUtcNow();
            if (Snapshot.DraftOperations.Any(operation => operation.Value.Status == GmailDraftOperationStatus.Pending))
            {
                await SaveGmailStateAsync(next => next.DraftOperations = [.. next.DraftOperations.Select(operation =>
                    operation.Value.Status == GmailDraftOperationStatus.Pending
                        ? operation with { Value = operation.Value with { Status = GmailDraftOperationStatus.Unknown,
                            Failure = Failure("outcome_unknown"), UpdatedAt = now, RetainUntil = null } }
                        : operation)]);
            }
            _recoveredDrafts = true;
        }
        PublishToolAvailability();
    }

    private CapabilityCaller RequireCaller()
    {
        var caller = CallerContextStamper.Require();
        if (BrainScope.Create(caller.AccountId, caller.BrainId).Id != Key.Brain)
        { throw new UnauthorizedAccessException("The selected Gmail connection belongs to another brain."); }
        return caller;
    }

    private string CallerScope()
    {
        var caller = RequireCaller();
        return JsonSerializer.Serialize(new[] { caller.PrincipalId, caller.AppId, caller.PersonId });
    }

    private IDisposable EnterGoogleTrace()
    {
        var caller = RequireCaller();
        var trace = TraceContext.Current;
        if (trace is not null && (trace.BrainId != Key.Brain || trace.PayerAccountId != caller.AccountId))
        { throw new UnauthorizedAccessException("The Gmail invocation trace does not belong to the trusted caller."); }
        return TraceContext.Enter(trace ?? TraceContext.Begin(caller.AccountId, Key.Brain, TraceContext.OriginOf(Key.Brain, FullKey)));
    }

    private GoogleCallContext NewCallContext(string? invocationId = null)
        => new(Funding.OwnConnection, new(Neuron: FullKey), invocationId ?? Guid.NewGuid().ToString("N"));

    private GmailFailure? GrantFailure(bool compose = false)
    {
        if (_grantAcceptancePending) { return Failure("provider_unavailable"); }
        if (!IsGrantReady) { return Failure("connection_required"); }
        var scopes = Snapshot.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!scopes.Contains(GmailScopes.Read, StringComparer.Ordinal)
            || compose && !scopes.Contains("https://www.googleapis.com/auth/gmail.compose", StringComparer.Ordinal))
        { return Failure("reconsent_required"); }
        return null;
    }

    private static GmailFailure Failure(string code, DateTimeOffset? retryAt = null) => new(code, code switch
    {
        "invalid_input" => "Check the request fields.",
        "connection_required" => "Connect the selected Gmail mailbox first.",
        "reconsent_required" => "Reconnect the selected mailbox with the required Gmail permissions.",
        "not_found" => "The item is not available to this caller. A missing receipt does not prove that no draft was created.",
        "rate_limited" => "The provider or connection is at its current work limit. Retry only when safe.",
        "preview_expired" => "This preview expired. Prepare a new preview before creating a draft.",
        "request_conflict" => "This request ID or preview is already bound to different draft work.",
        "outcome_unknown" => "The draft outcome cannot be confirmed durably. Do not automatically create another draft.",
        "budget_exhausted" => "The invocation has no remaining budget.",
        "cancelled_before_dispatch" => "The draft request was cancelled before dispatch.",
        _ => "Gmail is unavailable. Reads can be retried; check draft receipts before retrying a mutation.",
    }, [], retryAt);

    private static GmailFailure ProviderFailure(GoogleApiFailure failure) => Failure(failure.Kind switch
    {
        GoogleApiFailureKind.InvalidRequest => "invalid_input",
        GoogleApiFailureKind.ReconsentRequired or GoogleApiFailureKind.Forbidden => "reconsent_required",
        GoogleApiFailureKind.NotFound => "not_found",
        GoogleApiFailureKind.RateLimited => "rate_limited",
        _ => "provider_unavailable",
    }, failure.RetryAt);
}
