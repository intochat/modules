using Microsoft.Extensions.Configuration;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.Aspire;

[DigitalBrain.Module(ManifestName)]
public sealed class AspireModule : DigitalBrain.Module
{
    internal const string ManifestName = "aspire";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<AspireOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts => typeof(IDeployment).Assembly;

    // Candidate builds receive model credentials and their own fresh stores.
    public const string WorkdirField = "Workdir";
    public const string CandidateModelField = "CandidateModel";
    // Each role's front (its fixed port) as JSON, {role: url}: a roll reaches an instance the way the front does.
    public const string FrontsField = "Fronts";

    public static IntegrationSchema Integration { get; } = IntegrationSchema.For("aspire", "Aspire")
        .RequiresSetting(WorkdirField)
        .OffersSetting(FrontsField)
        .OffersSecret(CandidateModelField);

    // Discovery refuses a connector without an authentication method; the deployment's Workdir is its only credential.
    public static ConnectorDefinition Connector => new("aspire", "Aspire", ["deployment"], ["aspire"])
    {
        Neurons = [typeof(IAspire), typeof(ICandidate), typeof(IDeployment)],
        Signals = [typeof(AspireTelemetryChanged), typeof(CandidatePromoted), typeof(CandidateFailed), typeof(DeploymentChanged)],
    };

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton<Candidates.IProcessRunner, Candidates.SystemProcessRunner>();
        services.TryAddSingleton<Candidates.ICandidateHost, Candidates.DockerCandidateHost>();
        services.TryAddSingleton<Deployment.IClusterHost, Deployment.AspireCliClusterHost>();
        services.TryAddSingleton<Verification.ICandidateVerifier, Verification.CandidateVerifier>();
    }
}
