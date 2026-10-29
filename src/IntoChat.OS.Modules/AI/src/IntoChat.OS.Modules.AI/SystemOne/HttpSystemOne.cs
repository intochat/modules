using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Modules.AI.SystemOne;
using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.AI;

// Talks the TypeSafe System One wire shape: POST /v1/systemone with a bearer key. The endpoint is the
// integration's Endpoint setting when set, else the deployment default; so the hosted API and a local
// container exposing the same shape are interchangeable.
internal sealed class HttpSystemOne(
    IModuleIntegrations<AIModule> integrations,
    IOptions<AIOptions> options,
    IHttpClientFactory clients)
{
    internal const string IntegrationId = "typesafe";
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    public async Task<SystemOneResult> Ask(SystemOneRequest request, CancellationToken cancellationToken)
    {
        var handle = integrations.Get(IntegrationId);
        var snapshot = await handle.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != IntegrationStatus.Ready)
        { throw new ProviderUnavailableException(IntegrationId, snapshot.Status.ToString(), snapshot.MissingFields); }
        var released = await handle.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        var endpoint = snapshot.Settings.TryGetValue(AiIntegrations.EndpointField, out var configured) && configured.Length > 0
            ? configured
            : options.Value.SystemOne.Endpoint;
        var model = request.Model is { Length: > 0 } ? request.Model
            : options.Value.Default.SystemOne is { Length: > 0 } marker && SystemOneModel.FindByMarkerName(marker) is { } catalogued ? catalogued.Id
            : options.Value.SystemOne.Model;

        var questions = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, question) in request.Questions) { questions[key] = Body(question); }
        var payload = new { state = request.State, model, questions };

        using var http = clients.CreateClient(nameof(HttpSystemOne));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", released.Values[AiIntegrations.ApiKeyField]);
        using var response = await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/v1/systemone", payload, Json, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var parsed = await response.Content.ReadFromJsonAsync<Response>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("TypeSafe returned an empty response.");
        var answers = new Dictionary<string, SystemOneAnswer>(StringComparer.Ordinal);
        foreach (var (key, answer) in parsed.Answers) { answers[key] = Answer(answer); }
        return new SystemOneResult(parsed.Model, answers, parsed.Usage?.InputTokens ?? 0, parsed.Usage?.OutputTokens ?? 0);
    }

    private static object Body(SystemOneQuestion question) => question switch
    {
        SystemOneQuestion.Choice choice => new { type = "choice", instructions = choice.Instructions, criteria = choice.Criteria },
        SystemOneQuestion.Score score => new { type = "score", instructions = score.Instructions, criteria = score.Criteria },
        SystemOneQuestion.Noul noul => new { type = "noul", instructions = noul.Instructions },
        _ => throw new NotSupportedException($"{question.GetType().Name} is not a TypeSafe question."),
    };

    private static SystemOneAnswer Answer(AnswerDto answer) => answer.Type switch
    {
        "choice" => new SystemOneAnswer.Choice(answer.Choice ?? "", answer.Probabilities ?? [], answer.Confidence ?? 0),
        "score" => new SystemOneAnswer.Score(answer.Score ?? 0, answer.Probabilities ?? [], answer.Confidence ?? 0),
        "noul" => new SystemOneAnswer.Noul(answer.Noul ?? 0),
        _ => throw new InvalidOperationException($"TypeSafe answered an unknown question type '{answer.Type}'."),
    };

    private sealed record Response(string Model, Dictionary<string, AnswerDto> Answers, UsageDto? Usage);

    private sealed record AnswerDto(string Type, string? Choice, double? Score, double? Noul, double? Confidence, Dictionary<string, double>? Probabilities);

    private sealed record UsageDto(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}
