using CapabilityCaller = DigitalBrain.Capabilities.CapabilityCaller;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.AI;

// Reads registrations straight from their grains on every derivation: a capability appears the moment
// its provider becomes Ready and disappears the moment it stops, with no cache to go stale.
internal sealed class AiModelCapabilities(IModuleIntegrations<AIModule> integrations, IOptionsMonitor<AIOptions> options) : ICapabilitySource
{
    public const string LlmKind = "llm";

    public async Task<Capability[]> Derive(CapabilityCaller caller, CancellationToken ct)
    {
        var configuration = options.CurrentValue;
        var capabilities = new List<Capability>();
        foreach (var provider in LLMModel.All.Select(model => model.Provider).Distinct())
        {
            var integrationId = provider == AiProvider.Ollama ? "ollama" : AiIntegrations.IdOf(provider);
            if (provider != AiProvider.Ollama)
            {
                var snapshot = await integrations.Get(integrationId).ReadAsync(ct);
                if (snapshot.Status != IntegrationStatus.Ready) { continue; }
            }
            else if (string.IsNullOrEmpty(configuration.Ollama.Endpoint)) { continue; }

            capabilities.AddRange(LLMModel.All
                .Where(model => model.Provider == provider)
                // A local server being configured does not imply every compiled local model is installed.
                .Where(model => provider != AiProvider.Ollama || configuration.Ollama.Models.ContainsKey(model.Marker.Name))
                .Select(model => new Capability(integrationId, LlmKind, model.Marker.Name, model.Id)));
        }

        return [.. capabilities];
    }
}
