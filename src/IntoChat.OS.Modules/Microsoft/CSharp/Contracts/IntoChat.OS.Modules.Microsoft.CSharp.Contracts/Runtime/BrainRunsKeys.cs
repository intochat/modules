namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// The brain scope leads, so the framework's access check finds the owning brain from the key alone.
public static class BrainRunsKeys
{
    public static string For(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
        { throw new ArgumentException("A brain scope is a non-empty id without separators.", nameof(scope)); }
        return scope + "/runs";
    }
}
