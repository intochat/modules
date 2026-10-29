using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.LocalDisk.Tests.Unit;

public sealed class LocalVolumesFacts
{
    [Fact]
    public void FilterDropsPseudoFormatsAndMacSystemVolumesAndKeepsData()
    {
        foreach (var format in LocalVolumes.ExcludedFormats)
        {
            Assert.False(LocalVolumes.Include(Row("/mnt/" + format, format, ready: true, DriveType.Fixed, free: 10, total: 20)));
        }
        Assert.False(LocalVolumes.Include(Row("/System/Volumes/VM", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.False(LocalVolumes.Include(Row("/System/Volumes/Preboot", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.False(LocalVolumes.Include(Row("/System/Volumes/Update", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.False(LocalVolumes.Include(Row("/System/Volumes/xarts", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.False(LocalVolumes.Include(Row("/System/Volumes/iSCPreboot", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.False(LocalVolumes.Include(Row("/System/Volumes/Hardware", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.True(LocalVolumes.Include(Row("/System/Volumes/Data", "apfs", true, DriveType.Fixed, 10, 20)));
        Assert.True(LocalVolumes.Include(Row(@"C:\", "NTFS", true, DriveType.Fixed, 30, 100)));
    }

    [Fact]
    public void QuotaForUsesAvailableFreeSpace()
    {
        var quota = LocalVolumes.QuotaFor(@"C:\work", new FixedSource(Row(@"C:\", "NTFS", true, DriveType.Fixed, free: 30, total: 100)));
        Assert.Equal(QuotaState.Known, quota.State);
        Assert.Equal(70, quota.Used);
        Assert.Equal(100, quota.Total);
    }

    [Theory]
    [InlineData(false, DriveType.Fixed, 30, 100)]
    [InlineData(true, DriveType.Network, 30, 100)]
    [InlineData(true, DriveType.Fixed, -1, 100)]
    [InlineData(true, DriveType.Fixed, 150, 100)]
    [InlineData(true, DriveType.Fixed, 0, 0)]
    public void QuotaForUnknownWhenNotReadyNetworkOrUnusable(bool ready, DriveType type, long free, long total)
    {
        var quota = LocalVolumes.QuotaFor(@"C:\work", new FixedSource(Row(@"C:\", "NTFS", ready, type, free, total)));
        Assert.Equal(QuotaState.Unknown, quota.State);
        Assert.Null(quota.Used);
        Assert.Null(quota.Total);
    }

    [Fact]
    public void QuotaForUnknownOnIoAndAccessFailures()
    {
        Assert.Equal(QuotaState.Unknown, LocalVolumes.QuotaFor(@"C:\work", new ThrowingSource(new IOException())).State);
        Assert.Equal(QuotaState.Unknown, LocalVolumes.QuotaFor(@"C:\work", new ThrowingSource(new UnauthorizedAccessException())).State);
    }

    private static VolumeRow Row(string name, string format, bool ready, DriveType type, long free, long total)
        => new(name, format, ready, type, free, total);

    private sealed class FixedSource(VolumeRow row) : IVolumeSource
    {
        public IReadOnlyList<VolumeRow> Drives() => [row];
        public VolumeRow? ForPath(string path) => row;
    }

    private sealed class ThrowingSource(Exception error) : IVolumeSource
    {
        public IReadOnlyList<VolumeRow> Drives() => throw error;
        public VolumeRow? ForPath(string path) => throw error;
    }
}
