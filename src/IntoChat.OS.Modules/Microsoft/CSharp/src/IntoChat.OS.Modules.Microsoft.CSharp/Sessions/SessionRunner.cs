using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Azure.Core;
using IntoChat.OS.Client.Runtime.V1;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sessions;

// Where sessions live: the always-on sandbox container locally, or the Azure Container Apps session pool.
// Same host API behind both; the pool adds a bearer token, routes by the identifier query and manages
// session lifetime through its .management endpoints.
internal sealed class SessionRunner(HttpClient http, IOptions<CSharpOptions> options, TokenCredential? credential = null)
{
    private const string ManagementApiVersion = "2025-02-02-preview";
    private static readonly TokenRequestContext PoolScope = new(["https://dynamicsessions.io/.default"]);

    private bool Pooled => options.Value.SessionPoolEndpoint is { Length: > 0 };

    private Uri Base => new((Pooled ? options.Value.SessionPoolEndpoint! : options.Value.SandboxUrl
        ?? throw new InvalidOperationException("Configure csharp Deployment:SandboxUrl or Deployment:SessionPoolEndpoint.")).TrimEnd('/') + "/");

    public async Task Ensure(SessionStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        using var response = await SendAsync(HttpMethod.Post, Url("sessions/" + Identifier(start.Brain), start.Brain),
            JsonContent.Create(start, options: RuntimeProtocol.Json), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> Exists(string brain, CancellationToken cancellationToken)
    {
        var url = Pooled
            ? Url(".management/getSession", brain, "api-version=" + ManagementApiVersion)
            : Url("sessions/" + Identifier(brain), brain);
        using var response = await SendAsync(Pooled ? HttpMethod.Post : HttpMethod.Get, url, null, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task Stop(string brain, CancellationToken cancellationToken)
    {
        var url = Pooled
            ? Url(".management/stopSession", brain, "api-version=" + ManagementApiVersion)
            : Url("sessions/" + Identifier(brain) + "/stop", brain);
        using var response = await SendAsync(HttpMethod.Post, url, null, cancellationToken).ConfigureAwait(false);
        // The pool answers 400 SessionWithIdentifierNotFound and the local host 404 for a session that is already gone.
        if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.BadRequest)) { response.EnsureSuccessStatusCode(); }
    }

    public async Task<JudgeAnswer> Judge(string brain, JudgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await SendAsync(HttpMethod.Post, Url("sessions/" + Identifier(brain) + "/judge", brain),
            JsonContent.Create(request, options: RuntimeProtocol.Json), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JudgeAnswer>(RuntimeProtocol.Json, cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("The sandbox answered a judgement with nothing.");
    }

    // Pool identifiers allow a narrow character set, and brain ids should not leak into URLs.
    internal static string Identifier(string brain)
        => "b-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(brain)))[..32];

    private Uri Url(string path, string brain, string? extra = null)
        => new(Base, path + (Pooled ? "?identifier=" + Identifier(brain) + (extra is null ? "" : "&" + extra) : ""));

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri url, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (Pooled && credential is not null)
        { request.Headers.Authorization = new("Bearer", (await credential.GetTokenAsync(PoolScope, cancellationToken).ConfigureAwait(false)).Token); }
        return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
