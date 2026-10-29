using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.LocalDisk;

internal static class LocalDiskRoots
{
    internal const string NeedFullPath = "Enter a full folder path on this computer.";
    internal const string Unavailable = "The folder is unavailable on this computer.";

    internal static string? Syntax(string? path)
        => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ? NeedFullPath : null;

    internal static string Prepare(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Syntax(path) is not null) { throw new ArgumentException(NeedFullPath); }
        var full = PathConfinement.Normalize(path);
        if (!Directory.Exists(full)) { throw new DirectoryNotFoundException(); }
        _ = PathConfinement.Resolve([full], full);
        PathConfinement.RequireNoLinks(full);
        return full;
    }

    internal static string? FailureText(Exception error)
        => error is ArgumentException or IOException or UnauthorizedAccessException ? Unavailable : null;
}
