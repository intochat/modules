namespace IntoChat.OS.Modules.Microsoft.CSharp.Programs;

// Key formatting and parsing helper for app program neurons: "{brainId}/app/{appName}".
public static class AppProgramKeys
{
    public const string Prefix = "app";

    public static string For(string brainId, string appName)
    {
        if (string.IsNullOrWhiteSpace(brainId) || brainId.Length > 200 || brainId.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
        {
            throw new ArgumentException("A brain id is a non-empty string without separators.", nameof(brainId));
        }
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("An app name cannot be empty.", nameof(appName));
        }

        var cleanApp = appName;
        if (cleanApp.StartsWith(brainId + "/", StringComparison.OrdinalIgnoreCase))
        {
            cleanApp = cleanApp[(brainId.Length + 1)..];
        }
        if (cleanApp.StartsWith("apps/", StringComparison.OrdinalIgnoreCase))
        {
            cleanApp = cleanApp[5..];
        }
        else if (cleanApp.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
        {
            cleanApp = cleanApp[4..];
        }

        if (cleanApp.Length == 0 || cleanApp.Length > 100 || cleanApp.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
        {
            throw new ArgumentException("An app name is a non-empty identifier without separators.", nameof(appName));
        }

        return $"{brainId}/{Prefix}/{cleanApp}";
    }

    public static string For(string appKey)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            throw new ArgumentException("An app key cannot be empty.", nameof(appKey));
        }

        if (TryParse(appKey, out var brainId, out var appName))
        {
            return For(brainId, appName);
        }

        throw new ArgumentException($"'{appKey}' is not a valid app key.", nameof(appKey));
    }

    public static bool TryParse(string key, out string brainId, out string appName)
    {
        brainId = "";
        appName = "";
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var firstSlash = key.IndexOf('/');
        if (firstSlash <= 0)
        {
            return false;
        }

        var brain = key[..firstSlash];
        if (brain.Length > 200 || brain.AsSpan().IndexOfAny('\\', ':') >= 0)
        {
            return false;
        }

        var rest = key[(firstSlash + 1)..];
        if (rest.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
        {
            var name = rest[4..];
            if (name.Length == 0 || name.Length > 100 || name.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
            {
                return false;
            }
            brainId = brain;
            appName = name;
            return true;
        }

        if (rest.StartsWith("apps/", StringComparison.OrdinalIgnoreCase))
        {
            var name = rest[5..];
            if (name.Length == 0 || name.Length > 100 || name.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
            {
                return false;
            }
            brainId = brain;
            appName = name;
            return true;
        }

        if (rest.Length > 0 && rest.Length <= 100 && rest.AsSpan().IndexOfAny('/', '\\', ':') < 0)
        {
            if (brain.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                brain.Equals("apps", StringComparison.OrdinalIgnoreCase) ||
                brain.Equals("app", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            brainId = brain;
            appName = rest;
            return true;
        }

        return false;
    }

    public static (string BrainId, string AppName) Parse(string key)
    {
        if (TryParse(key, out var brainId, out var appName))
        {
            return (brainId, appName);
        }

        throw new ArgumentException($"'{key}' is not a valid app key; expected {{brain}}/app/{{name}}.", nameof(key));
    }

    public static string AppNameOf(string key) => Parse(key).AppName;

    public static string BrainOf(string key) => Parse(key).BrainId;
}
