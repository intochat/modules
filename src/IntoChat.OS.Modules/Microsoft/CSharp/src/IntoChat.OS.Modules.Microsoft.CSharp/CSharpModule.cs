using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Caching;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Authoring;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Edge;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using IntoChat.OS.Modules.Microsoft.CSharp.Files.Signals;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.CSharp;

[DigitalBrain.Module(ManifestName)]
public sealed class CSharpModule : DigitalBrain.Module, IHttpModule
{
    internal const string ManifestName = "csharp";

    // The runtime and its runs are still grains (06-06b: they wait for the legacy publishers); C# files are plain.
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<CSharpOptions>(IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId(ManifestName)))],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts => typeof(IntoChat.OS.Modules.Microsoft.CSharp.Files.ICSharpFile).Assembly;

    public static IntegrationSchema Integration => IntegrationSchema.For("csharp", "CSharp run tokens").RequiresSecret("RunTokenKey");

    // The sandbox is the external system behind ICSharpFile, the way Gmail is behind IGmail. Declared so the
    // catalog knows it, not offered to users: its only credential is the deployment's session-token key
    // (still named RunTokenKey), no connection neuron serves it, and Platform requires one auth method.
    public static ConnectorDefinition Connector { get; } = new("csharp", "C# sandbox", ["run-token"], [])
    {
        ExternalSystem = "C# sandbox",
        OptionalIntegrations = ["csharp"],
        Neurons = [typeof(ICSharpFile)],
        Signals = [typeof(CSharpFileChanged)],
    };

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = configuration.GetModuleOptions<CSharpOptions>("csharp");
        var authoring = options.Authoring;
        services.TryAddSingleton<CSharpToolService>();
        services.TryAddSingleton<IntoChat.OS.Modules.Apps.IScriptSandbox, CSharpScriptSandbox>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SessionTokens>();
        services.AddSingleton<IIntegrationStartup>(services => services.GetRequiredService<SessionTokens>());
        services.TryAddSingleton<CSharpContractDiscovery>();
        services.TryAddSingleton(new BoundedCache<string, CompiledBehavior>(CSharpScriptCheck.CacheCapacity, StringComparer.Ordinal));
        services.TryAddSingleton<CSharpScriptCheck>();
        services.TryAddSingleton<LocalBehaviorAssemblies>();
        services.TryAddSingleton<IBehaviorAssemblies>(provider =>
            provider.GetKeyedService<BlobServiceClient>(IntoChatOSNames.GrainState) is { } storage
                ? new BlobBehaviorAssemblies(storage)
                : provider.GetRequiredService<IOptions<CSharpOptions>>().Value.SandboxUrl is { Length: > 0 }
                    ? provider.GetRequiredService<LocalBehaviorAssemblies>() : new MemoryBehaviorAssemblies());
        // The session pool authenticates with the deployment's identity; the local container needs none.
        if (!string.IsNullOrWhiteSpace(options.SessionPoolEndpoint))
        { services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential()); }
        services.AddHttpClient<SessionRunner>();
        services.TryAddSingleton<SessionStreams>();
        services.TryAddKeyedSingleton<IHostApplicationLifetime>(typeof(CSharpModule),
            (services, _) => services.GetRequiredService<IHostApplicationLifetime>());
        services.TryAddSingleton<DeferredRuntimeStarts>();
        services.AddHostedService(services => services.GetRequiredService<DeferredRuntimeStarts>());
        services.TryAddSingleton<RuntimeDirectory>();
        services.TryAddSingleton<IWorkContinuation, RuntimeWorkContinuation>();
        services.TryAddTransient<RuntimeEdge>();
        services.AddSingleton<IIncomingGrainCallFilter, BrainRunsGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<DigitalBrain.Capabilities.Identity.ISignInHandler, RuntimeSignIn>());
        if (authoring)
        {
            services.TryAddScoped(provider => provider.GetRequiredService<CSharpToolService>().ForScope(BrainScope.CurrentId()));
        }
    }

    public void Configure(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapRuntimeEdge();
        LocalBehaviorAssemblies.Map(endpoints);
        if (endpoints.ServiceProvider.GetRequiredService<IOptions<CSharpOptions>>().Value.Authoring) { CSharpAuthoringEndpoints.Map(endpoints); }
    }
}
