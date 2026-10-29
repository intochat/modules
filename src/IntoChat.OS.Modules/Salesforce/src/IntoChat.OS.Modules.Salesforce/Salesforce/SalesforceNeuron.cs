using System.Text;
using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Modules.Salesforce.Signals;

namespace IntoChat.OS.Modules.Salesforce;

[DigitalBrain.Neuron("salesforce", State = "salesforce")]
internal sealed class SalesforceNeuron : ConnectionNeuron<SalesforceState>, ISalesforce
{
    private const int MaxWriteArgumentsBytes = 24 * 1024;
    private static readonly JsonSerializerOptions ArgumentJson = new(JsonSerializerDefaults.Web);

    // Salesforce's web-server flow omits expires_in. Without an expiry the base would never refresh
    // ahead of the session timeout, so a grant without one is taken to live this long.
    private static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromMinutes(60);

    private readonly ISalesforceProvider _provider;
    private readonly TimeProvider _time;

    public SalesforceNeuron(
        ISalesforceProvider provider,
        TimeProvider time)
    {
        _provider = provider;
        _time = time;
    }

    // The token response names the org: its instance URL must be HTTPS. Another org drops the old org's writes.
    protected override Task<OAuthGrant> AcceptGrantAsync(OAuthGrant grant)
    {
        Uri? url = null;
        try
        {
            using var json = JsonDocument.Parse(grant.Response);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("instance_url", out var instance) && instance.ValueKind == JsonValueKind.String)
            { _ = Uri.TryCreate(instance.GetString(), UriKind.Absolute, out url); }
        }
        catch (JsonException) { url = null; }
        if (url is null || url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0)
        { throw new SalesforceUnavailableException("Salesforce did not issue a valid HTTPS instance URL."); }
        if (Snapshot.Account != url.AbsoluteUri) { Snapshot.PendingWrite = null; Snapshot.SubmittingWrite = null; }
        Snapshot.Account = url.AbsoluteUri;
        return Task.FromResult(WithSessionExpiry(grant));
    }

    // Refresh grants never pass through AcceptGrantAsync, so they get the same default lifetime here.
    protected override async Task<OAuthGrant> RefreshAsync(Credential credential, CancellationToken ct)
        => WithSessionExpiry(await base.RefreshAsync(credential, ct));

    private OAuthGrant WithSessionExpiry(OAuthGrant grant)
        => grant.ExpiresAt is null ? grant with { ExpiresAt = _time.GetUtcNow() + DefaultSessionLifetime } : grant;

    // Salesforce, not the clock, says whether the stored token still works.
    protected override async Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct)
    {
        try
        {
            await _provider.InvokeAsync("getUserInfo", EmptyArguments(), credential.Value, ct);
            return ConnectionProbeResult.Connected();
        }
        catch (SalesforceNotConnectedException) { return ConnectionProbeResult.Expired("The Salesforce access token was refused."); }
        // These messages are the provider's own fixed sentences; they never carry the token.
        catch (Exception error) when (error is SalesforceUnavailableException or SalesforceUnreachableException)
        { return ConnectionProbeResult.Failing(error.Message); }
    }

    public async Task<SalesforceWritePreview> PrepareWrite(PrepareSalesforceWrite command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireConnection();
        if (command.Tool is not ("createRecord" or "updateRecord"))
        {
            throw new SalesforceUnavailableException("This Salesforce operation is not admitted.");
        }

        if (Encoding.UTF8.GetByteCount(command.Arguments) > MaxWriteArgumentsBytes)
        {
            throw new SalesforceUnavailableException("The complete Salesforce change must fit within 24 KiB.");
        }

        try
        {
            using var document = JsonDocument.Parse(command.Arguments);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new SalesforceUnavailableException("Salesforce write arguments must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            throw new SalesforceUnavailableException("Salesforce write arguments must be a JSON object.");
        }

        var schema = await ReadWriteSchemaAsync(command.Tool, CancellationToken.None);
        if (schema.Reason is { } reason)
        {
            throw new SalesforceUnavailableException(reason);
        }

        var preview = new SalesforceWritePreview(
            Guid.NewGuid().ToString("n"), command.Tool, schema.SchemaHash!, command.Arguments, _time.GetUtcNow().AddMinutes(10));
        Snapshot.PendingWrite = preview;
        await Commit(Snapshot, new SalesforceWritePrepared(preview));
        return preview;
    }

    public async Task<SalesforceWritePreview> ConfirmWrite(ConfirmSalesforceWrite command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireConnection();
        var preview = Snapshot.PendingWrite;
        if (preview is null || preview.PreviewId != command.PreviewId || preview.ExpiresAt <= _time.GetUtcNow()
            || string.IsNullOrEmpty(preview.ToolSchemaHash) || preview.ToolSchemaHash != command.ToolSchemaHash)
        {
            throw new SalesforceUnavailableException("The Salesforce preview expired or changed. Prepare a fresh preview.");
        }

        var schema = await ReadWriteSchemaAsync(preview.Tool, CancellationToken.None);
        if (schema.Reason is not null)
        {
            Snapshot.PendingWrite = null;
            await SaveQuietly(Snapshot);
            await PublishAsync(new SalesforceWriteUncertain(preview.PreviewId));
            return preview;
        }

        if (schema.SchemaHash != preview.ToolSchemaHash)
        {
            throw new SalesforceUnavailableException("The Salesforce preview schema changed. Prepare a fresh preview.");
        }

        Snapshot.PendingWrite = null;
        Snapshot.SubmittingWrite = preview;
        await SaveQuietly(Snapshot);
        try
        {
            var recordId = await SubmitWriteAsync(preview, CancellationToken.None);
            return preview with { RecordId = recordId };
        }
        finally
        {
            Snapshot.SubmittingWrite = null;
            await SaveQuietly(Snapshot);
        }
    }

    public async Task<SalesforceQueryResult> Query(SoqlQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        try
        {
            SalesforceQueryGuard.Validate(query.Query);
        }
        catch (ArgumentException)
        {
            throw new SalesforceUnavailableException("Use one SELECT with an outer WHERE and positive LIMIT. Comments, multiple statements and locking queries are not allowed.");
        }

        var result = await ReadAsync("soqlQuery", JsonSerializer.SerializeToElement(query, ArgumentJson), cancellationToken);
        return new SalesforceQueryResult(result.GetRawText(), result.GetProperty("totalSize").GetInt32());
    }

    public async Task<SalesforceSchema> ReadSchema(ReadSalesforceSchema query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var objectName = query.ObjectName;
        if (objectName is not null && (objectName.Length is < 1 or > 255 || !objectName.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')))
        {
            throw new SalesforceUnavailableException("Use a Salesforce object API name.");
        }

        var arguments = objectName is null
            ? EmptyArguments()
            : JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["object-name"] = objectName });
        return new SalesforceSchema((await ReadAsync("getObjectSchema", arguments, cancellationToken)).GetRawText());
    }

    public async Task<SalesforceUserInfo> ReadUserInfo(CancellationToken cancellationToken = default)
        => new((await ReadAsync("getUserInfo", EmptyArguments(), cancellationToken)).GetRawText());

    private async Task<string?> SubmitWriteAsync(SalesforceWritePreview preview, CancellationToken cancellationToken)
    {
        try
        {
            using var arguments = JsonDocument.Parse(preview.Arguments);
            var result = await WithTokenAsync((token, ct) => _provider.InvokeAsync(preview.Tool, arguments.RootElement, token, ct), cancellationToken);
            if (result.ValueKind != JsonValueKind.Object
                || result.TryGetProperty("id", out var record) && record.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                throw new SalesforceUnavailableException("Salesforce returned an invalid response shape.");
            }

            if (result.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True)
            {
                await PublishAsync(new SalesforceWriteFailed(preview.PreviewId));
                return null;
            }

            var recordId = result.TryGetProperty("id", out var id) ? id.GetString() : null;
            await PublishAsync(new RecordWritten(preview with { RecordId = recordId }));
            return recordId;
        }
        catch (Exception error) when (error is SalesforceNotConnectedException or SalesforceUnavailableException)
        {
            await PublishAsync(new SalesforceWriteUncertain(preview.PreviewId));
            return null;
        }
    }

    // A refused refresh has already marked the connection Expired; a provider refusal marks it Failing.
    private async Task<(string? SchemaHash, string? Reason)> ReadWriteSchemaAsync(string tool, CancellationToken cancellationToken)
    {
        try
        {
            return (await WithTokenAsync((token, ct) => _provider.ReadToolSchemaHashAsync(tool, token, ct), cancellationToken), null);
        }
        catch (SalesforceNotConnectedException error)
        {
            return (null, error.Message);
        }
        catch (Exception error) when (error is SalesforceUnavailableException or SalesforceUnreachableException)
        {
            await MarkFailing(error.Message);
            return (null, error.Message);
        }
    }

    private Task<JsonElement> ReadAsync(string tool, JsonElement arguments, CancellationToken cancellationToken)
    {
        RequireConnection();
        return WithTokenAsync((token, ct) => _provider.InvokeAsync(tool, arguments, token, ct), cancellationToken);
    }

    // Every provider call opens the token through the base, which refreshes it first when it is about
    // to lapse. A refresh that fails marks the connection, so its status says why calls stop working.
    private async Task<T> WithTokenAsync<T>(Func<string, CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        var opened = false;
        try
        {
            return await UseCredentialAsync((credential, ct) =>
            {
                opened = true;
                return call(credential.Value, ct);
            }, cancellationToken);
        }
        catch (Exception error) when (!opened && error is not OperationCanceledException)
        {
            // A token endpoint reply may carry a secret; only the exception type is kept.
            var detail = $"Salesforce refused the token refresh ({error.GetType().Name}). Reconnect Salesforce.";
            await (error is HttpRequestException ? MarkFailing(detail) : MarkExpired(detail));
            throw new SalesforceNotConnectedException("The Salesforce token refresh failed. Reconnect Salesforce.");
        }
    }

    // Expired or Failing still serve: a refresh or a later probe moves the status back to Connected.
    private void RequireConnection()
    {
        // Reading Key refuses a key that is not a canonical connection key naming this connector.
        _ = Key;
        if (Snapshot.Status == ConnectionStatus.Disconnected || Snapshot.Secrets.Length == 0 || Snapshot.Account is null)
        {
            throw new SalesforceNotConnectedException();
        }
    }

    private static JsonElement EmptyArguments()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
