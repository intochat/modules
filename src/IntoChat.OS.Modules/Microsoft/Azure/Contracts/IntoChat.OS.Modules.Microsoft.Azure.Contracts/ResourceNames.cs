namespace IntoChat.OS.Modules.Microsoft.Azure;

// <prefix>-<env>-<kind>, so a name follows from the config alone. Storage accounts and vaults have
// their own limits (no dashes, 24 characters) and keep the same order.
public static class ResourceNames
{
    public static string For(string prefix, string env, string kind) => $"{prefix}-{env}-{kind}";

    public static string ResourceGroup(string prefix, string env) => For(prefix, env, "rg");

    public static string Identity(string prefix, string env) => For(prefix, env, "id");

    public static string LogWorkspace(string prefix, string env) => For(prefix, env, "log");

    public static string KeyVault(string prefix, string env) => Limit(For(prefix, env, "kv"), 24);

    public static string Postgres(string prefix, string env) => For(prefix, env, "pg");

    // Registry names are alphanumeric, 5 to 50 characters.
    public static string Registry(string prefix, string env) =>
        Limit(new string([.. $"{prefix}{env}acr".ToLowerInvariant().Where(char.IsAsciiLetterOrDigit)]), 50);

    public static string Environment(string prefix, string env) => For(prefix, env, "env");

    public static string App(string prefix, string env, string app) => For(prefix, env, app);

    public static string StorageAccount(string prefix, string env)
    {
        var name = new string([.. $"{prefix}{env}st".ToLowerInvariant().Where(char.IsAsciiLetterOrDigit)]);
        return Limit(name, 24);
    }

    private static string Limit(string name, int length) => name.Length <= length ? name : name[..length].TrimEnd('-');
}
