using System.ClientModel;
using IntoChat.OS.Modules.AI.Models;
using OpenAI;
using OpenAI.Audio;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.AI.Media.OpenAI;

internal sealed class OpenAISpeechSynthesis(string model, IAiCredentials credentials) : ISpeechSynthesisTransport
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => credentials.IsReady(AiIntegrations.IdOf(AiProvider.OpenAI), cancellationToken);
    public string Model => model;
    public async Task<MediaPayload> Synthesize(SpeechSynthesisRequest request, CancellationToken cancellationToken)
    {
        var integration = AiIntegrations.IdOf(AiProvider.OpenAI);
        var material = await credentials.MaterializeAsync(integration, cancellationToken);
        var apiKey = material.Values[AiIntegrations.ApiKeyField];
        var clientOptions = new OpenAIClientOptions();
        if (material.Snapshot.Settings.GetValueOrDefault(AiIntegrations.EndpointField) is { Length: > 0 } endpoint) { clientOptions.Endpoint = new Uri(endpoint); }
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions).GetAudioClient(model);
        var response = await client.GenerateSpeechAsync(request.Text, new GeneratedSpeechVoice(request.Voice),
            new SpeechGenerationOptions { ResponseFormat = GeneratedSpeechFormat.Mp3 }, cancellationToken);
        return new MediaPayload(response.Value.ToArray(), "audio/mpeg");
    }
}
