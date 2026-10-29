using System.Security.Cryptography;
using System.Text.Json;

namespace IntoChat.OS.Modules.Google.Gmail;

internal static class GmailPageTokens
{
    private sealed record Continuation(string ProviderToken, string Connection, string Mailbox, long Generation);

    internal static string Write(string providerToken, string connection, string mailbox, long generation, ReadOnlySpan<byte> key)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Continuation(providerToken, connection, mailbox, generation));
        var token = Encode(bytes) + "." + Encode(HMACSHA256.HashData(key, bytes));
        if (token.Length > GmailValidation.MaximumPageTokenLength) { throw new InvalidDataException("The provider continuation exceeded the supported size."); }
        return token;
    }

    internal static bool TryRead(string token, string connection, string mailbox, long generation, ReadOnlySpan<byte> key, out string? providerToken)
    {
        providerToken = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > GmailValidation.MaximumPageTokenLength || key.Length != 32) { return false; }
        var separator = token.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator != token.LastIndexOf('.')) { return false; }
        try
        {
            var bytes = Decode(token[..separator]);
            var signature = Decode(token[(separator + 1)..]);
            if (signature.Length != 32 || !CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(key, bytes))) { return false; }
            var value = JsonSerializer.Deserialize<Continuation>(bytes);
            if (value is null || value.Connection != connection || value.Generation != generation
                || !string.Equals(value.Mailbox, mailbox, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(value.ProviderToken)) { return false; }
            providerToken = value.ProviderToken;
            return true;
        }
        catch (FormatException) { return false; }
        catch (JsonException) { return false; }
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }
}
