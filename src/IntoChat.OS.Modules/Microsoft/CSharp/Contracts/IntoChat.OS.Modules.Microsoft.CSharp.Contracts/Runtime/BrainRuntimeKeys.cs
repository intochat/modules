namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// The brain scope leads, so the framework's access check finds the owning brain from the key alone.
public static class BrainRuntimeKeys
{
    // The runtime's id inside its brain.
    public const string Name = "runtime";

    public static string For(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
        { throw new ArgumentException("A brain scope is a non-empty id without separators.", nameof(scope)); }
        return scope + "/" + Name;
    }
}
