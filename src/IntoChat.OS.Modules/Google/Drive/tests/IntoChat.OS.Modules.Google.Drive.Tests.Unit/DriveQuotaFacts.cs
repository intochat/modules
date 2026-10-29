using System.Net;
using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.Google.Drive.Tests.Unit;

// D-11: Drive quota from about.get(storageQuota) is tri-state and pooled; D-16: Drive HTTP failures map to typed storage errors.
public sealed class DriveQuotaFacts
{
    [Fact]
    public void ALimitIsAKnownPooledQuota()
    {
        var quota = DriveQuota.From("""{"storageQuota":{"limit":"100","usage":"40"}}""");
        Assert.Equal((QuotaState.Known, 40L, 100L, true), (quota.State, quota.Used, quota.Total, quota.Pooled));
    }

    [Fact]
    public void NoLimitIsUnlimited()
    {
        var quota = DriveQuota.From("""{"storageQuota":{"usage":"40"}}""");
        Assert.Equal((QuotaState.Unlimited, 40L, null, true), (quota.State, quota.Used, quota.Total, quota.Pooled));
    }

    [Theory]
    [InlineData("""{"user":{"emailAddress":"a@b.c"}}""")]
    [InlineData("""{"storageQuota":{"limit":"lots","usage":"40"}}""")]
    [InlineData("""{"storageQuota":{"limit":"100"}}""")]
    [InlineData("not json")]
    public void AMissingOrUnreadableQuotaIsUnknown(string json)
    {
        var quota = DriveQuota.From(json);
        Assert.Equal(QuotaState.Unknown, quota.State);
        Assert.Null(quota.Total);
        Assert.Null(quota.Used);
    }

    [Fact]
    public void AnAccountOverItsQuotaIsKnownAndFull()
    {
        var quota = DriveQuota.From("""{"storageQuota":{"limit":"100","usage":"140"}}""");
        Assert.Equal((100L, 100L), (quota.Used!.Value, quota.Total!.Value));
    }
}

public sealed class DriveErrorsFacts
{
    [Theory]
    [InlineData(404, null, "not-found")]
    [InlineData(401, null, "needs-auth")]
    [InlineData(403, "storageQuotaExceeded", "quota-exceeded")]
    [InlineData(403, "insufficientFilePermissions", "denied")]
    [InlineData(403, "insufficientPermissions", "unsupported")]
    [InlineData(403, "rateLimitExceeded", "unavailable")]
    [InlineData(429, null, "unavailable")]
    [InlineData(503, null, "unavailable")]
    public void StatusAndReasonMapToTheStorageError(int status, string? reason, string code)
        => Assert.Equal(code, DriveErrors.Map((HttpStatusCode)status, reason, new ItemId("file")).Code);

    [Fact]
    public void TheReasonComesFromTheErrorBodyAndNeverTheMessage()
    {
        Assert.Equal("storageQuotaExceeded", DriveErrors.ReasonOf("""{"error":{"code":403,"errors":[{"reason":"storageQuotaExceeded"}],"message":"Bearer secret"}}"""));
        Assert.Null(DriveErrors.ReasonOf("<html>"));
        Assert.DoesNotContain("secret", DriveErrors.Map(HttpStatusCode.Forbidden, "storageQuotaExceeded", new ItemId("file")).Message, StringComparison.Ordinal);
    }
}
