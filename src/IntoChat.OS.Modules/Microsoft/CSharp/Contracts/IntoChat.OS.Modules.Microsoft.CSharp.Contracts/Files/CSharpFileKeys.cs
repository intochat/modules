namespace IntoChat.OS.Modules.Microsoft.CSharp.Files;

// The one grammar for an ICSharpFile key. The brain scope leads, so the framework's access check
// finds the owning brain from the key alone; "catalog" is the catalog neuron's segment, not a file.
public static class CSharpFileKeys
{
    public static string For(string scope, string name)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > 200 || scope.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
        { throw new ArgumentException("A brain scope is a non-empty id without separators.", nameof(scope)); }
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name == "catalog"
            || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
        { throw new ArgumentException("Use a C# file name of 1–100 ASCII letters, digits, hyphens or underscores.", nameof(name)); }
        return $"{scope}/csharp/{name}";
    }
}
