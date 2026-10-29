using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Unit;

// D-11, CAP-04: a Windows PC's quota sums the distinct volumes of its allowed roots, used = TotalSize - TotalFreeSpace; anything
// uncertain is Unknown, never 0 and never an exception.
public sealed class LocalQuotaFacts
{
    [Fact]
    public void OneRootOnAKnownVolumeIsKnownUsedAndTotal()
    {
        var quota = LocalQuota.For([@"C:\data"], new FakeProbe(new() { [@"C:\"] = new(true, 100, 40) }));
        Assert.Equal((QuotaState.Known, 60L, 100L), (quota.State, quota.Used, quota.Total));
    }

    [Fact]
    public void TwoRootsOnOneVolumeCountItOnce()
    {
        var quota = LocalQuota.For([@"C:\a", @"c:\b"], new FakeProbe(new() { [@"C:\"] = new(true, 100, 40) }));
        Assert.Equal((60L, 100L), (quota.Used!.Value, quota.Total!.Value));
    }

    [Fact]
    public void TwoVolumesSum()
    {
        var quota = LocalQuota.For([@"C:\a", @"D:\b"], new FakeProbe(new() { [@"C:\"] = new(true, 100, 40), [@"D:\"] = new(true, 50, 10) }));
        Assert.Equal((QuotaState.Known, 100L, 150L), (quota.State, quota.Used, quota.Total));
    }

    [Theory]
    [InlineData("io")]
    [InlineData("denied")]
    [InlineData("missing")]
    public void AProbeFailureIsUnknown(string failure)
    {
        Exception error = failure switch
        {
            "io" => new IOException("device"),
            "denied" => new UnauthorizedAccessException("no"),
            _ => new DriveNotFoundException("gone"),
        };
        Assert.Equal(QuotaState.Unknown, LocalQuota.For([@"C:\a"], new FakeProbe([], error)).State);
    }

    [Theory]
    [InlineData(false, 100, 40)]
    [InlineData(true, 0, 0)]
    [InlineData(true, -1, 0)]
    public void ANotReadyOrSizelessVolumeIsUnknown(bool ready, long total, long free)
    {
        var quota = LocalQuota.For([@"C:\a", @"D:\b"], new FakeProbe(new() { [@"C:\"] = new(true, 100, 40), [@"D:\"] = new(ready, total, free) }));
        Assert.Equal(QuotaState.Unknown, quota.State);
        Assert.Null(quota.Total);
    }

    [Fact]
    public void NoRootsIsUnknown() => Assert.Equal(QuotaState.Unknown, LocalQuota.For([], new FakeProbe([])).State);

    private sealed class FakeProbe(Dictionary<string, VolumeFacts> volumes, Exception? failure = null) : IVolumeProbe
    {
        public string? VolumeOf(string path) => path.Length >= 2 && path[1] == ':' ? char.ToUpperInvariant(path[0]) + @":\" : null;

        public VolumeFacts Probe(string volumeRoot)
            => failure is not null ? throw failure : volumes.First(pair => string.Equals(pair.Key, volumeRoot, StringComparison.OrdinalIgnoreCase)).Value;
    }
}
