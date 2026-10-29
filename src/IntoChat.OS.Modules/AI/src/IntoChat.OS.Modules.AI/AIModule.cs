using DigitalBrain.Capabilities;
using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using IntoChat.OS;
using IntoChat.OS.Modules.AI.Agents;
using IntoChat.OS.Modules.AI.Media;
using IntoChat.OS.Modules.AI.Routing;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Modules.AI.Pricing;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Connections.Contracts;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using IntoChat.OS.Connections.Surface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Hosting;

namespace IntoChat.OS.Modules.AI;

[DigitalBrain.Module(ModuleName)]
public sealed class AIModule : DigitalBrain.Module, IHttpModule
{
    public static System.Reflection.Assembly? Contracts { get; } = typeof(IntoChat.OS.Modules.AI.Agents.Context.IAgentContext).Assembly;

    public static IntegrationSchema[] Integrations => AiIntegrations.All;
    public static IntegrationSchema OpenAIIntegration => AiIntegrations.OpenAI;
    public static IntegrationSchema OpenRouterIntegration => AiIntegrations.OpenRouter;
    public static IntegrationSchema TypeSafeIntegration => AiIntegrations.TypeSafe;
    public static ConnectorDefinition[] Connectors => ModelConnectors.All;

    internal const string ModuleName = "ai";

    // R4-04: what the module needs from its host. Registrations of neurons that are still grains stay here.
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<AIOptions>(IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId(ModuleName)))],
        Secrets: []);

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = AIOptionsRuntime.Read(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAiCredentials, RegistrationCredentials>();
        services.TryAddSingleton<BrainKeyAvailability>();
        services.TryAddSingleton<ProviderPublicEndpoints>();
        services.TryAddSingleton<IPriceList, ModelPriceList>();
        AIClients.Add(services);
        services.AddHttpClient();
        services.TryAddSingleton<HttpSystemOne>();
        services.TryAddSingleton<AppOperationCatalog>();
        services.AddHostedService<AppMenuHost>();
        services.TryAddSingleton<ModelCatalog>();
        services.TryAddSingleton<InferenceService>();
        services.TryAddSingleton<IModelInference>(provider => provider.GetRequiredService<InferenceService>());
        services.TryAddSingleton<NeuronSurface>();
        services.AddSingleton<ICapabilitySource, AiModelCapabilities>();
        services.AddMediaNeurons();
        AIClients.AddImageGeneration(services, options);
        VoiceToTextHosting.Add(services, options);
    }

    // The calling brain's model list for the shell's model picker; never endpoints or secrets. The
    // brain is the edge's stamped caller; without one the list is the deployment's.
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/ai/models", async (INeuronProxies brain) => Results.Ok(await brain.Get<IModelCatalog>(
            CatalogKey(CallerContextStamper.TryGet(out var caller) ? caller : null)).List()));
    }

    // A caller stamped without a valid brain (or no caller) gets the deployment's list, not a 500.
    internal static string CatalogKey(CapabilityCaller? caller)
        => caller is not null && BrainScope.IsValidId(caller.BrainId) && !string.IsNullOrWhiteSpace(caller.AccountId)
            ? IModelCatalog.Key(caller.BrainId) : IModelCatalog.DeploymentKey;
}
