namespace IntoChat.OS.Modules.Microsoft.Azure.Install;

// The ARM id a resource gets, computed from its names so a dry run and the foundation refs need no Azure.
public static class ResourceIds
{
    public static string For(string subscription, string kind, string resourceGroup, string name)
    {
        var group = $"/subscriptions/{subscription}/resourceGroups/{resourceGroup}";
        return kind switch
        {
            ResourceKinds.ResourceGroup => group,
            ResourceKinds.Identity => Provider(group, "Microsoft.ManagedIdentity/userAssignedIdentities", name),
            ResourceKinds.LogWorkspace => Provider(group, "Microsoft.OperationalInsights/workspaces", name),
            ResourceKinds.StorageAccount => Provider(group, "Microsoft.Storage/storageAccounts", name),
            ResourceKinds.KeyVault => Provider(group, "Microsoft.KeyVault/vaults", name),
            ResourceKinds.ManagedEnvironment => Provider(group, "Microsoft.App/managedEnvironments", name),
            ResourceKinds.ContainerApp => Provider(group, "Microsoft.App/containerApps", name),
            ResourceKinds.SessionPool => Provider(group, "Microsoft.App/sessionPools", name),
            ResourceKinds.Registry => Provider(group, "Microsoft.ContainerRegistry/registries", name),
            ResourceKinds.Postgres => Provider(group, "Microsoft.DBforPostgreSQL/flexibleServers", name),
            ResourceKinds.NetworkSecurityGroup => Provider(group, "Microsoft.Network/networkSecurityGroups", name),
            ResourceKinds.VirtualNetwork => Provider(group, "Microsoft.Network/virtualNetworks", name),
            _ => $"{group}/providers/IntoChat.OS/{kind}/{name}",
        };
    }

    public static string For(string subscription, ResourceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec switch
        {
            SubnetSpec subnet => $"{For(subscription, ResourceKinds.VirtualNetwork, spec.ResourceGroup, subnet.VirtualNetwork)}/subnets/{spec.Name}",
            PostgresDatabaseSpec database => $"{For(subscription, ResourceKinds.Postgres, spec.ResourceGroup, database.Server)}/databases/{spec.Name}",
            _ => For(subscription, spec.Kind, spec.ResourceGroup, spec.Name),
        };
    }

    private static string Provider(string group, string type, string name) => $"{group}/providers/{type}/{name}";
}
