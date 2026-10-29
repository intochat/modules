using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.AI.Media;

internal sealed class UnavailableSpeechSynthesis : ISpeechSynthesisTransport
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public string Model => "";
    public Task<MediaPayload> Synthesize(SpeechSynthesisRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Configure a speech synthesis transport.");
}
