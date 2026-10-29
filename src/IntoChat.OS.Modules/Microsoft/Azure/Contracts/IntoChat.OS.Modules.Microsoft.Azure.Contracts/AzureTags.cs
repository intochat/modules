namespace IntoChat.OS.Modules.Microsoft.Azure;

public static class AzureTags
{
    public const string ManagedBy = "managed-by";
    public const string ManagedByValue = "intochat";
    public const string Env = "env";
    public const string Owner = "owner";

    // The spec's own tags win over the three standard ones.
    public static Dictionary<string, string> Merge(ResourceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var tags = new Dictionary<string, string>
        {
            [ManagedBy] = ManagedByValue,
            [Env] = spec.Env,
            [Owner] = spec.Owner,
        };
        foreach (var (name, value) in spec.Tags) { tags[name] = value; }
        return tags;
    }

    public static ResourceSpec Tagged(ResourceSpec spec) => spec with { Tags = Merge(spec) };
}
