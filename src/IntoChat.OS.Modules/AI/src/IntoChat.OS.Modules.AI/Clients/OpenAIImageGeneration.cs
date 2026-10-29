using System.ClientModel;
using IntoChat.OS.Modules.AI.Media;
using IntoChat.OS.Modules.AI.Models;
using OpenAI;
using OpenAI.Images;

namespace IntoChat.OS.Modules.AI;

internal sealed class OpenAIImageGeneration(ImageModel model, IAiCredentials credentials) : IImageGeneration, IConditionallyAvailable
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => credentials.IsReady(AiIntegrations.IdOf(model.Provider), cancellationToken);

    public async Task<GeneratedUiImage> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var material = await credentials.MaterializeAsync(AiIntegrations.IdOf(model.Provider), cancellationToken).ConfigureAwait(false);
        var apiKey = material.Values[AiIntegrations.ApiKeyField];
        var options = new OpenAIClientOptions();
        if (material.Snapshot.Settings.GetValueOrDefault(AiIntegrations.EndpointField) is { Length: > 0 } endpoint) { options.Endpoint = new Uri(endpoint); }
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options).GetImageClient(model.Id);
        var image = await client.GenerateImageAsync(
            prompt,
            OptionsFor(model),
            cancellationToken).ConfigureAwait(false);

        var bytes = image.Value.ImageBytes
            ?? throw new InvalidOperationException(
                $"{model.DisplayName} returned no image bytes. A model that answers with a URL must set "
                + $"{nameof(ImageModel.AcceptsResponseFormat)} so bytes are requested explicitly.");

        return new GeneratedUiImage(bytes.ToArray(), model.MediaType, model.Id);
    }

    // Asking gpt-image-1 for a response format is HTTP 400 unknown_parameter: it
    // always answers with base64. Only models that accept the option get it.
    internal static ImageGenerationOptions OptionsFor(ImageModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.AcceptsResponseFormat
            ? new ImageGenerationOptions { ResponseFormat = GeneratedImageFormat.Bytes }
            : new ImageGenerationOptions();
    }
}
