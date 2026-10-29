using System.Text;
using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.LocalDisk;

/// <summary>An opaque id: <c>ld:</c> plus the base64url of <c>{rootId}:{relative}</c>. Relative paths use <c>/</c>.</summary>
internal static class LocalItemId
{
    internal const string Prefix = "ld:";

    internal static ItemId Of(string rootId, string relative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        var text = rootId + ":" + (relative ?? "").Replace('\\', '/');
        return new ItemId(Prefix + Base64Url(Encoding.UTF8.GetBytes(text)));
    }

    internal static (string RootId, string Relative) Read(ItemId id)
    {
        if (id.Value is not { } value || !value.StartsWith(Prefix, StringComparison.Ordinal)) { throw new NotFound(id); }
        string text;
        try { text = Encoding.UTF8.GetString(Base64UrlDecode(value[Prefix.Length..])); }
        catch (FormatException) { throw new NotFound(id); }
        var colon = text.IndexOf(':');
        if (colon <= 0) { throw new NotFound(id); }
        var rootId = text[..colon];
        var relative = text[(colon + 1)..];
        if (rootId.Length == 0) { throw new NotFound(id); }
        if (Hostile(relative)) { throw new Denied(id); }
        return (rootId, relative);
    }

    internal static bool Hostile(string relative)
    {
        if (relative.Length == 0) { return false; }
        if (relative[0] is '/' or '\\') { return true; }
        if (relative.Length >= 2 && relative[1] == ':') { return true; }
        foreach (var part in relative.Split(['/', '\\']))
        {
            if (part.Length == 0 || part is "." or "..") { return true; }
        }
        return false;
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
