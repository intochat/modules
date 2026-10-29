using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.Roslyn.Tests;

public sealed partial class RoslynFixture
{
    // The workspace services the module registers, without a solution to open.
    private static void ConfigureBoth(IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SolutionWorkspace>();
        services.TryAddSingleton<ISolutionLoader, MSBuildSolutionLoader>();
        services.TryAddSingleton<CodeFixCatalog>();
        services.TryAddSingleton<ChangeSetEditor>();
    }
}
