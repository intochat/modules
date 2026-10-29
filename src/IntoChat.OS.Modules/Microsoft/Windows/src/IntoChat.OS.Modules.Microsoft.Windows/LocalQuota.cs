using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// One volume as the quota sees it.
internal sealed record VolumeFacts(bool IsReady, long TotalSize, long TotalFreeSpace);

// Reads a volume by its root ("C:\"). The default wraps System.IO.DriveInfo; facts substitute their own, on any OS.
internal interface IVolumeProbe
{
    // The volume holding path ("C:\" for "C:\data"), or null when it has none.
    string? VolumeOf(string path);

    VolumeFacts Probe(string volumeRoot);
}

internal sealed class DriveInfoProbe : IVolumeProbe
{
    public static DriveInfoProbe Instance { get; } = new();

    public string? VolumeOf(string path) => Path.GetPathRoot(path);

    public VolumeFacts Probe(string volumeRoot)
    {
        var drive = new DriveInfo(volumeRoot);
        return drive.IsReady ? new VolumeFacts(true, drive.TotalSize, drive.TotalFreeSpace) : new VolumeFacts(false, 0, 0);
    }
}

// The quota of a Windows PC storage (D-11, CAP-04): the distinct volumes holding the allowed roots, each counted once, used =
// TotalSize - TotalFreeSpace. Anything uncertain (no roots, a probe failure, a volume not ready, a size of 0 or less) is
// Quota.Unknown: never 0, never an exception.
internal static class LocalQuota
{
    public static Quota For(IReadOnlyList<string> roots, IVolumeProbe probe)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(probe);
        if (roots.Count == 0) { return Quota.Unknown; }
        long used = 0, total = 0;
        try
        {
            foreach (var volume in roots.Select(probe.VolumeOf).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(volume)) { return Quota.Unknown; }
                var facts = probe.Probe(volume);
                if (!facts.IsReady || facts.TotalSize <= 0 || facts.TotalFreeSpace < 0 || facts.TotalFreeSpace > facts.TotalSize) { return Quota.Unknown; }
                checked
                {
                    used += facts.TotalSize - facts.TotalFreeSpace;
                    total += facts.TotalSize;
                }
            }
        }
        // DriveInfo throws IOException (DriveNotFoundException included) for a volume that vanished or is not ready.
        catch (IOException) { return Quota.Unknown; }
        catch (UnauthorizedAccessException) { return Quota.Unknown; }
        catch (ArgumentException) { return Quota.Unknown; }
        catch (OverflowException) { return Quota.Unknown; }
        catch (System.Security.SecurityException) { return Quota.Unknown; }
        return Quota.Known(used, total);
    }
}
