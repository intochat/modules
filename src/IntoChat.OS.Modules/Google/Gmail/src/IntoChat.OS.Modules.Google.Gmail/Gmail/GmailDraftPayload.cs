using MimeKit;

namespace IntoChat.OS.Modules.Google.Gmail;

internal static class GmailDraftPayload
{
    internal static async Task<byte[]> EncodeAsync(string[] to, string[] cc, string[] bcc, string subject, string body, CancellationToken ct)
    {
        using var message = new MimeMessage { Subject = subject, Body = new TextPart("plain") { Text = body, ContentTransferEncoding = ContentEncoding.Base64 } };
        foreach (var recipient in to) { message.To.Add(MailboxAddress.Parse(recipient)); }
        foreach (var recipient in cc) { message.Cc.Add(MailboxAddress.Parse(recipient)); }
        foreach (var recipient in bcc) { message.Bcc.Add(MailboxAddress.Parse(recipient)); }
        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, ct);
        return stream.ToArray();
    }
}
