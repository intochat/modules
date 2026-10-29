using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Microsoft.Azure.Install;

public sealed record ModuleDeploymentBinding(string ModuleId, IModuleDeployment Deployment);

// Finds the IModuleDeployment of each composed module in the module's own assembly, in composition order.
public static class ModuleDeployments
{
    public static IReadOnlyList<ModuleDeploymentBinding> Discover(IEnumerable<OsModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var composed = modules.GroupBy(module => module.Type).Select(group => group.First()).ToArray();
        var types = composed.Select(module => module.Type).ToHashSet();
        var found = new List<ModuleDeploymentBinding>();
        foreach (var assembly in composed.Select(module => module.Type.Assembly).Distinct())
        {
            foreach (var type in assembly.GetExportedTypes().Where(t => !t.IsAbstract && typeof(IModuleDeployment).IsAssignableFrom(t)))
            {
                var deployment = (IModuleDeployment)Activator.CreateInstance(type)!;
                // A deployment for a module that is not composed is a stale reference: the installer
                // deploys what the OS composes, and only that.
                if (!types.Contains(deployment.Module))
                {
                    throw new InvalidOperationException($"The deployment '{type.Name}' is for module '{deployment.Module.Name}', which is not composed.");
                }
                found.Add(new ModuleDeploymentBinding(composed.First(module => module.Type == deployment.Module).Id.Value, deployment));
            }
        }
        return [.. composed.SelectMany(module => found.Where(binding => binding.Deployment.Module == module.Type))];
    }
}
