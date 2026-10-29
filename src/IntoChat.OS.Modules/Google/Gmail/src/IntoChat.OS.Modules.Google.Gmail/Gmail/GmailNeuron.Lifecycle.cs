using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Compute;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Connections.Platform.Identity;
using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed partial class GmailNeuron
{
    internal const string LifecycleReminder = "gmail-lifecycle";
    private static readonly TimeSpan LifecyclePeriod = TimeSpan.FromMinutes(1);
    private const string WatchDetailPrefix = "Gmail push watch ";

    protected override async Task<OAuthGrant> AcceptGrantAsync(OAuthGrant grant)
    {
        using var trace = EnterGoogleTrace();
        var result = await Api.ProfileAsync(grant.AccessToken, NewCallContext(), CancellationToken.None);
        if (result.Value is not { } profile || !GmailPubSub.ValidMailbox(profile.EmailAddress))
        { throw new GmailUnavailableException("Google did not verify the mailbox identity. Reconnect Gmail."); }
        PrepareGrantBinding(profile.EmailAddress, grant.AuthMethodId);
        Snapshot.History = new(profile.EmailAddress, Snapshot.GrantGeneration, profile.HistoryId, null, null, false, null);
        Snapshot.Watch = null;
        return grant;
    }

    protected override async Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct)
    {
        using var trace = EnterGoogleTrace();
        var result = await Api.ProfileAsync(credential.Value, NewCallContext(), ct);
        if (result.Value is { } profile)
        {
            return string.Equals(profile.EmailAddress, Snapshot.Account, StringComparison.OrdinalIgnoreCase)
                ? ConnectionProbeResult.Connected() : ConnectionProbeResult.Expired("The Gmail token belongs to another mailbox.");
        }
        return result.Failure!.Kind is GoogleApiFailureKind.ReconsentRequired or GoogleApiFailureKind.Forbidden
            ? ConnectionProbeResult.Expired("The Gmail access token was refused.")
            : ConnectionProbeResult.Failing($"Google could not complete the profile check (HTTP {result.Failure.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable"}).");
    }

    protected override async Task<OAuthGrant> RefreshAsync(Credential credential, CancellationToken ct)
    {
        try
        {
            var grant = await base.RefreshAsync(credential, ct);
            using var response = JsonDocument.Parse(grant.Response);
            return response.RootElement.TryGetProperty("scope", out _) ? grant : grant with { Scopes = Snapshot.Scopes };
        }
        catch (OAuthTokenExchangeException error)
        { throw new GmailCredentialException(error.Failure == OAuthTokenFailure.ReconsentRequired ? "reconsent_required" : "provider_unavailable"); }
        catch (JsonException) { throw new GmailCredentialException("provider_unavailable"); }
    }

    protected override async Task OnConnectedAsync()
    {
        CompleteGrantBinding();
        await RecoverLifecycleAsync(CancellationToken.None);
        using var trace = EnterGoogleTrace();
        await RenewWatchIfDueAsync(CancellationToken.None);
    }

    protected override async Task OnDisconnectedAsync()
    {
        await SaveGmailStateAsync(next =>
        {
            next.Watch = null;
            next.History = null;
            next.BoundMailbox = null;
            next.GrantGeneration = checked(next.GrantGeneration + 1);
            next.DraftPreviews = [];
        });
        await RemoveReminderAsync();
        // users.stop affects the OAuth application's mailbox watch, including other brains. The remote lease expires.
    }

    internal async Task RecoverLifecycleAsync(CancellationToken ct)
    {
        await EnsureGrantBindingAsync();
        if (!IsGrantReady) { await RemoveReminderAsync(); return; }
        if (Snapshot.History is not { } history || history.GrantGeneration != Snapshot.GrantGeneration
            || !string.Equals(history.Mailbox, Snapshot.BoundMailbox, StringComparison.OrdinalIgnoreCase))
        { await SaveGmailStateAsync(next => next.History = new(next.BoundMailbox!, next.GrantGeneration, null, null, null, false, null)); }
        var topic = await ReadWatchTopicAsync(ct);
        if (Snapshot.Watch is not { } watch || watch.GrantGeneration != Snapshot.GrantGeneration || watch.TopicName != topic)
        {
            await SaveGmailStateAsync(next => next.Watch = new(next.BoundMailbox!, next.GrantGeneration, topic, null,
                topic is null ? null : GmailClock.GetUtcNow(), topic is null ? GmailWatchStatus.Unconfigured : GmailWatchStatus.RetryPending));
        }
        if (topic is null) { await SetWatchDetailAsync(WatchDetailPrefix + "is disabled because Google Pub/Sub is not configured."); }
        await RestoreReminderAsync();
    }

    // The lifecycle reminder is the runtime host's durable reminder (06-08: a plain neuron's Context.Reminders).
    protected override async Task OnReminder(string reminderName, CancellationToken cancellationToken)
    {
        if (reminderName != LifecycleReminder) { return; }
        if (!IsGrantReady) { await RemoveReminderAsync(); return; }
        var trace = Snapshot.History?.Wakeup?.Trace ?? Snapshot.History?.Work?.Trace;
        await ServiceProvider.GetRequiredService<IBrainOwnerDispatch>().RunAsync(Key.Brain, FullKey, async () =>
        {
            await RecoverLifecycleAsync(CancellationToken.None);
            if (Snapshot.History is { } history && (history.Wakeup is not null || history.Work is not null)
                && (history.RetryAt is null || history.RetryAt <= GmailClock.GetUtcNow()))
            { await ProcessHistoryAsync(CancellationToken.None); }
            await RenewWatchIfDueAsync(CancellationToken.None);
            await RestoreReminderAsync();
        }, trace, CancellationToken.None);
    }

    public async Task<GmailPushAcceptance> AcceptWatchPush(GmailWatchPush push)
    {
        RequireCaller();
        if (!GmailPubSub.ValidMailbox(push.EmailAddress) || !GmailPubSub.ValidHistoryId(push.HistoryId))
        { return GmailPushAcceptance.Invalid; }
        await EnsureGrantBindingAsync();
        if (!IsGrantReady || !string.Equals(Snapshot.BoundMailbox, push.EmailAddress, StringComparison.OrdinalIgnoreCase))
        { return GmailPushAcceptance.Ignored; }
        using var trace = EnterGoogleTrace();
        await RecoverLifecycleAsync(CancellationToken.None);
        await DrainWakeupAsync();
        var history = Snapshot.History!;
        if (history.Cursor is { } cursor && GmailPubSub.HistoryNumber(push.HistoryId) <= GmailPubSub.HistoryNumber(cursor))
        { return GmailPushAcceptance.Duplicate; }
        var work = history.Work is { } pending
            ? pending with { TargetHistoryId = GmailPubSub.HistoryNumber(push.HistoryId) > GmailPubSub.HistoryNumber(pending.TargetHistoryId) ? push.HistoryId : pending.TargetHistoryId }
            : new GmailHistoryWork(history.Cursor ?? "", push.HistoryId, null, TraceContext.Current!, Guid.NewGuid().ToString("N"));
        await SaveGmailStateAsync(next => next.History = history with { Work = work, RetryAt = history.Work is null ? null : history.RetryAt });
        await RestoreReminderAsync();
        if (Snapshot.History!.RetryAt > GmailClock.GetUtcNow()) { return GmailPushAcceptance.Retryable; }
        return await ProcessHistoryAsync(CancellationToken.None);
    }

    private async Task<GmailPushAcceptance> ProcessHistoryAsync(CancellationToken ct)
    {
        await DrainWakeupAsync();
        if (Snapshot.History?.Work is not { } work) { return GmailPushAcceptance.Duplicate; }
        ValidateLifecycleTrace(work.Trace);
        using var trace = TraceContext.Enter(work.Trace);
        try
        {
            if (work.StartHistoryId.Length == 0) { return await ResyncHistoryAsync(work, ct); }
            for (var page = 0; page < 10; page++)
            {
                var result = await UseCredentialAsync((credential, token) => Api.HistoryAsync(credential.Value,
                    NewCallContext(work.InvocationId), work.StartHistoryId, work.PageToken, 100, token), ct);
                if (result.Value is not { } value)
                {
                    if (result.Failure!.Kind == GoogleApiFailureKind.NotFound) { return await ResyncHistoryAsync(work, ct); }
                    return await HistoryFailureAsync(result.Failure);
                }
                if (value.NextPageToken is null)
                {
                    if (GmailPubSub.HistoryNumber(value.HistoryId) < GmailPubSub.HistoryNumber(work.StartHistoryId))
                    { return await HistoryFailureAsync(new(GoogleApiFailureKind.InvalidResponse)); }
                    await CommitHistoryAsync(value.HistoryId, work, false);
                    return GmailPushAcceptance.Accepted;
                }
                work = work with { PageToken = value.NextPageToken };
                await SaveGmailStateAsync(next => next.History = next.History! with { Work = work, RetryAt = null });
            }
            return GmailPushAcceptance.Accepted;
        }
        catch (GmailCredentialException error)
        {
            return await HistoryFailureAsync(new(error.Code == "reconsent_required"
                ? GoogleApiFailureKind.ReconsentRequired : GoogleApiFailureKind.Unavailable));
        }
    }

    private async Task<GmailPushAcceptance> ResyncHistoryAsync(GmailHistoryWork work, CancellationToken ct)
    {
        var result = await UseCredentialAsync((credential, token) => Api.ProfileAsync(credential.Value, NewCallContext(work.InvocationId), token), ct);
        if (result.Value is not { } profile) { return await HistoryFailureAsync(result.Failure!); }
        if (!string.Equals(profile.EmailAddress, Snapshot.BoundMailbox, StringComparison.OrdinalIgnoreCase))
        { return await HistoryFailureAsync(new(GoogleApiFailureKind.ReconsentRequired)); }
        await CommitHistoryAsync(profile.HistoryId, work, true);
        await MarkFailing("Gmail history expired or was unavailable. A fresh baseline was recorded; bounded mailbox reconciliation is needed for earlier changes.");
        return GmailPushAcceptance.Accepted;
    }

    private async Task CommitHistoryAsync(string historyId, GmailHistoryWork work, bool resync)
    {
        var behindTarget = GmailPubSub.HistoryNumber(historyId) < GmailPubSub.HistoryNumber(work.TargetHistoryId);
        await SaveGmailStateAsync(next =>
        {
            var history = next.History!;
            var advanced = history.Cursor is null || GmailPubSub.HistoryNumber(historyId) != GmailPubSub.HistoryNumber(history.Cursor);
            next.History = history with
            {
                Cursor = historyId,
                Work = behindTarget ? work with { StartHistoryId = historyId, PageToken = null } : null,
                Wakeup = advanced || resync ? new(historyId, work.Trace) : null,
                ResyncRequired = history.ResyncRequired || resync,
                RetryAt = behindTarget ? GmailClock.GetUtcNow().Add(LifecyclePeriod) : null,
            };
        });
        await DrainWakeupAsync();
        await RestoreReminderAsync();
    }

    private async Task DrainWakeupAsync()
    {
        if (Snapshot.History?.Wakeup is not { } wakeup) { return; }
        ValidateLifecycleTrace(wakeup.Trace);
        using var trace = TraceContext.Enter(wakeup.Trace);
        await PublishAsync(new MailReceived(Snapshot.History.Mailbox, wakeup.HistoryId));
        await SaveGmailStateAsync(next => next.History = next.History! with { Wakeup = null });
    }

    private async Task<GmailPushAcceptance> HistoryFailureAsync(GoogleApiFailure failure)
    {
        await SaveGmailStateAsync(next => next.History = next.History! with
        { RetryAt = failure.RetryAt ?? GmailClock.GetUtcNow().Add(LifecyclePeriod) });
        if (failure.Kind is GoogleApiFailureKind.ReconsentRequired or GoogleApiFailureKind.Forbidden)
        {
            await MarkExpired("Google refused the Gmail token. Reconnect Gmail.");
            return GmailPushAcceptance.Ignored;
        }
        return GmailPushAcceptance.Retryable;
    }

    private async Task RenewWatchIfDueAsync(CancellationToken ct)
    {
        if (!IsGrantReady || Snapshot.Watch is not { TopicName: { } topic, NextActionAt: { } due } watch
            || due > GmailClock.GetUtcNow()) { return; }
        GoogleApiResult<GoogleWatch> result;
        try
        {
            result = await UseCredentialAsync((credential, token) => Api.WatchAsync(credential.Value, NewCallContext(), topic, token), ct);
        }
        catch (GmailCredentialException error)
        {
            result = new(null, new(error.Code == "reconsent_required" ? GoogleApiFailureKind.ReconsentRequired : GoogleApiFailureKind.Unavailable), null);
        }
        var now = GmailClock.GetUtcNow();
        if (result.Value is { } value && value.ExpiresAt > now)
        {
            var dueAt = value.ExpiresAt.AddHours(-1);
            if (dueAt > now.AddDays(1)) { dueAt = now.AddDays(1); }
            if (dueAt <= now) { dueAt = now.Add(LifecyclePeriod); }
            await SaveGmailStateAsync(next =>
            {
                next.Watch = watch with { ExpiresAt = value.ExpiresAt, NextActionAt = dueAt, Status = GmailWatchStatus.Active };
                if (next.History is { Cursor: null } history) { next.History = history with { Cursor = value.HistoryId }; }
            });
            await SetWatchDetailAsync(null);
        }
        else if (result.Failure?.Kind == GoogleApiFailureKind.ReconsentRequired)
        {
            await SaveGmailStateAsync(next => next.Watch = watch with
            { NextActionAt = null, Status = GmailWatchStatus.Stopped });
            await MarkExpired("Google refused the Gmail token. Reconnect Gmail.");
        }
        else
        {
            await SaveGmailStateAsync(next => next.Watch = watch with
            { NextActionAt = result.Failure?.RetryAt ?? now.Add(LifecyclePeriod), Status = GmailWatchStatus.RetryPending });
            await SetWatchDetailAsync(WatchDetailPrefix + "could not be renewed. A retry is scheduled.");
        }
        await RestoreReminderAsync();
    }

    private async Task<string?> ReadWatchTopicAsync(CancellationToken ct)
    {
        var integration = await ServiceProvider.GetRequiredService<IModuleIntegrations<GmailModule>>().Get("google-pubsub").ReadAsync(ct);
        return integration.Status == IntegrationStatus.Ready && integration.Settings.TryGetValue("TopicName", out var topic)
            && !string.IsNullOrWhiteSpace(topic) ? topic : null;
    }

    private async Task RestoreReminderAsync()
    {
        var needed = IsGrantReady && (Snapshot.Watch is { TopicName: not null, NextActionAt: not null }
            || Snapshot.History is { Work: not null } or { Wakeup: not null });
        if (!needed) { await RemoveReminderAsync(); return; }
        if (!(await Context.Reminders.Names()).Contains(LifecycleReminder))
        { await Context.Reminders.Start(LifecycleReminder, LifecyclePeriod, LifecyclePeriod); }
    }

    private async Task RemoveReminderAsync()
    {
        if ((await Context.Reminders.Names()).Contains(LifecycleReminder)) { await Context.Reminders.Stop(LifecycleReminder); }
    }

    private Task SetWatchDetailAsync(string? detail)
        => Snapshot.Detail == detail || Snapshot.Detail is { } existing && !existing.StartsWith(WatchDetailPrefix, StringComparison.Ordinal)
            ? Task.CompletedTask : SaveGmailStateAsync(next => next.Detail = detail);

    private void ValidateLifecycleTrace(Trace trace)
    {
        var caller = RequireCaller();
        if (trace.BrainId != Key.Brain || trace.PayerAccountId != caller.AccountId)
        { throw new UnauthorizedAccessException("The stored Gmail work belongs to another owner."); }
    }

    // Stored Gmail work resumes on activation (06-08: a plain neuron's OnActivate replaces the activation handler). Activation runs on
    // the host grain's scheduler (DigitalBrain, plan 04-20), so the lifecycle reminder is read and set right here.
    protected override Task Activated(CancellationToken cancellationToken) => RecoverLifecycleAsync(cancellationToken);
}
