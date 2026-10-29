using IntoChat.OS.Modules.Flutter.Button;

namespace IntoChat.OS.Modules.Flutter;

internal static class ControlVisualValidation
{
    public static void Check(ControlVisual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (visual.Svg?.Length > 32_000 || visual.Icon?.Length > 80 || visual.ImageUrl?.Length > 4096)
        { throw new ArgumentException("Control artwork exceeds its size limit."); }
        if (visual.Tone is not ("neutral" or "accent")) { throw new ArgumentException("Unknown control tone."); }
        if (!string.IsNullOrEmpty(visual.ImageUrl) &&
            (!Uri.TryCreate(visual.ImageUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
        { throw new ArgumentException("Control images require an HTTP or HTTPS URL."); }
    }
}
