using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Framework.Modules.Validation;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed record GmailPubSubPush(GmailPubSubMessage? Message, string? Subscription);
internal sealed record GmailPubSubMessage(string? Data, string? MessageId);

internal sealed class GmailPubSubValidator : IModuleValidator<GmailPubSubPush>
{
    public ValidationIssue[] Validate(GmailPubSubPush value) => GmailPubSub.TryUnwrap(value, out _)
        ? [] : [new("message", "invalid_push", "Provide a bounded Gmail notification envelope.")];
}

internal static class GmailPubSub
{
    public const int MaxEnvelopeBytes = 16 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    internal static bool ValidHistoryId(string? value) => value is { Length: > 0 and <= 20 }
        && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    internal static ulong HistoryNumber(string value) => ulong.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    internal static bool ValidMailbox(string? value) => value is { Length: > 0 and <= 320 }
        && value.Contains('@', StringComparison.Ordinal) && !value.Any(char.IsControl) && !value.Any(char.IsWhiteSpace);

    public static bool TryUnwrap(GmailPubSubPush envelope, [NotNullWhen(true)] out GmailWatchPush? push)
    {
        push = null;
        if (envelope.Message is not { Data: { Length: > 0 and <= 8192 } data, MessageId.Length: > 0 and <= 256 }
            || envelope.Subscription is not { Length: > 0 and <= 1024 }
            || data.Any(char.IsWhiteSpace)) { return false; }
        try
        {
            var normalized = data.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            var bytes = Convert.FromBase64String(normalized);
            if (bytes.Length > 4096) { return false; }
            push = JsonSerializer.Deserialize<GmailWatchPush>(StrictUtf8.GetString(bytes), PayloadJson);
            return push is not null && ValidMailbox(push.EmailAddress) && ValidHistoryId(push.HistoryId);
        }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
        catch (JsonException) { return false; }
    }
}
