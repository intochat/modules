using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Compute;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Framework.Modules.Metering;
using IntoChat.OS.Modules.Google.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Google.Drive;

[GenerateSerializer, Alias("db.google-drive.state")]
internal sealed class DriveState : ConnectionState;

// One Google Drive as storage. Locations are Drive file ids; "root" is My Drive.
// Google documents are exported as plain text; other Google formats are not text. storage@1 lives in DriveStorage.cs (05-09).
[DigitalBrain.Neuron(DriveScopes.Connector, State = "google-drive")]
internal sealed partial class DriveNeuron()
    : ConnectionNeuron<DriveState>
{
    private const string Root = "root";
    private const string RootName = "My Drive";
    private const string FolderType = "application/vnd.google-apps.folder";
    private const string DocumentType = "application/vnd.google-apps.document";
    private const int MaxDepth = 32;

    protected override async Task<OAuthGrant> AcceptGrantAsync(OAuthGrant grant)
    {
        using var about = await AboutAsync(grant.AccessToken, CancellationToken.None);
        if (!about.IsSuccessStatusCode) { throw new DriveUnavailableException("Google did not verify the Drive account. Reconnect Google Drive."); }
        using var json = JsonDocument.Parse(await about.Content.ReadAsStringAsync());
        Snapshot.Account = json.RootElement.TryGetProperty("user", out var user) && user.TryGetProperty("emailAddress", out var email) ? email.GetString() : null;
        if (string.IsNullOrWhiteSpace(Snapshot.Account)) { throw new DriveUnavailableException("Google did not verify the Drive account. Reconnect Google Drive."); }
        return grant;
    }

    protected override async Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct)
    {
        using var about = await AboutAsync(credential.Value, ct);
        return about.StatusCode switch
        {
            HttpStatusCode.OK => ConnectionProbeResult.Connected(),
            HttpStatusCode.Unauthorized => ConnectionProbeResult.Expired("The Google Drive access token was refused."),
            var status => ConnectionProbeResult.Failing($"Google answered the Drive check with HTTP {(int)status}."),
        };
    }

    private async Task<JsonDocument> GetJsonAsync(string path, string operation, CancellationToken ct) =>
        await SendAsync(path, operation, ct, async response => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct));

    private async Task<T> SendAsync<T>(string path, string operation, CancellationToken ct, Func<HttpResponseMessage, Task<T>> read)
    {
        using var trace = EnterTrace();
        return await UseCredentialAsync(async (credential, token) =>
        {
            using var response = await RequestAsync(new Uri(Options.Endpoint, path), operation, credential.Value, token);
            if (response.StatusCode == HttpStatusCode.NotFound) { throw new FileNotFoundException("This Drive item does not exist or is not shared with this account."); }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await MarkExpired("The Google Drive access token was refused. Reconnect Google Drive.");
                throw new UnauthorizedAccessException("Google refused the Drive access token. Reconnect Google Drive.");
            }
            if (!response.IsSuccessStatusCode) { throw new DriveUnavailableException($"Google Drive answered HTTP {(int)response.StatusCode}."); }
            return await read(response);
        }, ct);
    }

    private Task<HttpResponseMessage> AboutAsync(string accessToken, CancellationToken ct) =>
        RequestAsync(new Uri(Options.Endpoint, "about?fields=user(emailAddress)"), GoogleDriveOperations.About, accessToken, ct);

    private async Task<HttpResponseMessage> RequestAsync(Uri uri, string operation, string accessToken, CancellationToken ct, RangeHeaderValue? range = null,
        HttpMethod? method = null, HttpContent? content = null)
    {
        using var trace = EnterTrace();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        using var client = ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(GoogleHttp.ClientName);
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Range = range;
        request.SetExternalCall(new(operation, Funding.OwnConnection, new(Neuron: FullKey), Guid.NewGuid().ToString("N")));
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
    }

    private IDisposable? EnterTrace()
    {
        var caller = CallerContextStamper.Require();
        if (BrainScope.Create(caller.AccountId, caller.BrainId).Id != Key.Brain)
        { throw new UnauthorizedAccessException("The Drive connection belongs to another brain."); }
        return TraceContext.Current is null
            ? TraceContext.Enter(TraceContext.Begin(caller.AccountId, Key.Brain, TraceContext.OriginOf(Key.Brain, FullKey))) : null;
    }

    private DriveModuleOptions Options => ServiceProvider.GetRequiredService<DriveModuleOptions>();
}
