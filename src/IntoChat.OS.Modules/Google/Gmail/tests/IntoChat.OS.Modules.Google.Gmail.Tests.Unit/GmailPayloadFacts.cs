using System.Text;
using MimeKit;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

public sealed class GmailPayloadFacts
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void AContinuationIsAuthenticatedAndBoundToItsMailboxGrant()
    {
        var token = GmailPageTokens.Write("opaque+/provider==", "brain/gmail/primary", "fixture@example.test", 7, Key);
        Assert.True(GmailPageTokens.TryRead(token, "brain/gmail/primary", "fixture@example.test", 7, Key, out var provider));
        Assert.Equal("opaque+/provider==", provider);
        Assert.False(GmailPageTokens.TryRead(token, "another/gmail/primary", "fixture@example.test", 7, Key, out _));
        Assert.False(GmailPageTokens.TryRead(token, "brain/gmail/primary", "other@example.test", 7, Key, out _));
        Assert.False(GmailPageTokens.TryRead(token, "brain/gmail/primary", "fixture@example.test", 8, Key, out _));
        Assert.False(GmailPageTokens.TryRead(token, "brain/gmail/primary", "fixture@example.test", 7, new byte[32], out _));
        var parts = token.Split('.');
        parts[0] = (parts[0][0] == 'A' ? "B" : "A") + parts[0][1..];
        Assert.False(GmailPageTokens.TryRead(string.Join('.', parts), "brain/gmail/primary", "fixture@example.test", 7, Key, out _));
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(5900)]
    public void EveryEmittedContinuationCanBeRead(int length)
    {
        var provider = new string('x', length);
        var token = GmailPageTokens.Write(provider, "brain/gmail/primary", "fixture@example.test", 7, Key);
        Assert.True(GmailPageTokens.TryRead(token, "brain/gmail/primary", "fixture@example.test", 7, Key, out var actual));
        Assert.Equal(provider, actual);
    }

    [Fact]
    public void AnOversizedContinuationIsRejectedBeforeItCanBeReturned()
        => Assert.Throws<InvalidDataException>(() => GmailPageTokens.Write(new string('x', 8192), "brain/gmail/primary", "fixture@example.test", 7, Key));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-provider-token")]
    [InlineData("a.b.c")]
    [InlineData("%%.%%")]
    public void MalformedContinuationIsAnExpectedValidationFailure(string token)
        => Assert.False(GmailPageTokens.TryRead(token, "brain/gmail/primary", "fixture@example.test", 1, Key, out _));

    [Fact]
    public async Task MimeEncodingRoundTripsHeadersRecipientsAndUnicodeBody()
    {
        var bytes = await GmailDraftPayload.EncodeAsync(["Example Person <to@example.test>"], ["cc@example.test"], ["hidden@example.test"],
            "Résumé ✓", "Hello, κόσμε!\nSecond line.", TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(bytes);
        using var mime = await MimeMessage.LoadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal("Résumé ✓", mime.Subject);
        Assert.Equal("to@example.test", Assert.Single(mime.To.Mailboxes).Address);
        Assert.Equal("cc@example.test", Assert.Single(mime.Cc.Mailboxes).Address);
        Assert.Equal("hidden@example.test", Assert.Single(mime.Bcc.Mailboxes).Address);
        Assert.NotNull(mime.TextBody);
        Assert.Equal("Hello, κόσμε!\nSecond line.", mime.TextBody.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty(mime.Attachments);
        Assert.DoesNotContain("Résumé", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptySubjectAndBodyRemainValidMime()
    {
        var bytes = await GmailDraftPayload.EncodeAsync(["to@example.test"], [], [], "", "", TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(bytes);
        using var mime = await MimeMessage.LoadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal("", mime.Subject);
        Assert.Equal("", mime.TextBody);
    }
}
