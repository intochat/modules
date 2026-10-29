using System.Text;
using System.Text.Json;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

public sealed class GmailPubSubFacts
{
    [Theory]
    [InlineData("1")]
    [InlineData("18446744073709551615")]
    public void ValidNumericHistoryCanArriveAsBase64UrlWithoutPadding(string history)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { emailAddress = "mail@example.test", historyId = history })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.True(GmailPubSub.TryUnwrap(new(new(data, "message-1"), "projects/test/subscriptions/gmail"), out var push));
        Assert.Equal(history, push.HistoryId);
    }

    [Theory]
    [InlineData("not-numeric")]
    [InlineData("-1")]
    [InlineData("18446744073709551616")]
    [InlineData("1\n")]
    public void InvalidHistoryNeverBecomesDomainWork(string history)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { emailAddress = "mail@example.test", historyId = history })));
        Assert.False(GmailPubSub.TryUnwrap(new(new(data, "message-1"), "projects/test/subscriptions/gmail"), out _));
    }

    [Fact]
    public void InvalidUtf8AndUnboundedFieldsAreRejected()
    {
        var prefix = Encoding.UTF8.GetBytes("{\"emailAddress\":\"");
        var suffix = Encoding.UTF8.GetBytes("@example.test\",\"historyId\":\"1\"}");
        Assert.False(GmailPubSub.TryUnwrap(new(new(Convert.ToBase64String([.. prefix, 0xff, .. suffix]), "1"), "subscription"), out _));
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { emailAddress = new string('x', 321), historyId = "1" })));
        Assert.False(GmailPubSub.TryUnwrap(new(new(data, "1"), "subscription"), out _));
        Assert.False(GmailPubSub.TryUnwrap(new(new(new string('A', 8193), "1"), "subscription"), out _));
    }
}
