using System.Globalization;
using System.Text;
using System.Text.Json;
using MimeKit;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed class GoogleInvalidResponseException : Exception;

internal static class GoogleApiReader
{
    private const int MaxResponseBytes = 4 * 1024 * 1024;
    private const int MaxBodyBytes = 256 * 1024;

    public static async Task<JsonDocument> Read(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > MaxResponseBytes) { throw new GoogleInvalidResponseException(); }
        await using var input = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(buffer, ct);
            if (count == 0) { break; }
            if (output.Length + count > MaxResponseBytes) { throw new GoogleInvalidResponseException(); }
            output.Write(buffer, 0, count);
        }
        var document = JsonDocument.Parse(output.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
        if (document.RootElement.ValueKind == JsonValueKind.Object) { return document; }
        document.Dispose();
        throw new GoogleInvalidResponseException();
    }

    public static string? Optional(JsonElement root, string name, int maximum = 1024)
    {
        if (root.ValueKind != JsonValueKind.Object) { throw new GoogleInvalidResponseException(); }
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) { return null; }
        if (value.ValueKind != JsonValueKind.String) { throw new GoogleInvalidResponseException(); }
        var text = value.GetString()!;
        if (text.Length > maximum) { throw new GoogleInvalidResponseException(); }
        return text;
    }

    public static string Required(JsonElement root, string name, int maximum = 1024)
        => Optional(root, name, maximum) is { } value && !string.IsNullOrWhiteSpace(value) ? value : throw new GoogleInvalidResponseException();

    public static ulong ParseHistoryId(string id)
        => id.Length is > 0 and <= 20 && ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number : throw new GoogleInvalidResponseException();

    public static string HistoryId(JsonElement root)
    {
        var value = Required(root, "historyId", 20);
        ParseHistoryId(value);
        return value;
    }

    public static GoogleHistoryPage History(JsonElement root, ulong start, int take)
    {
        var id = HistoryId(root);
        if (ParseHistoryId(id) < start) { throw new GoogleInvalidResponseException(); }
        var records = Array(root, "history", take);
        var prior = start;
        foreach (var record in records)
        {
            var current = ParseHistoryId(Required(record, "id", 20));
            if (current < prior || current > ParseHistoryId(id)) { throw new GoogleInvalidResponseException(); }
            prior = current;
        }
        return new(id, Optional(root, "nextPageToken", 8192), records.Length);
    }

    public static GoogleWatch Watch(JsonElement root)
    {
        var expiry = Required(root, "expiration", 20);
        if (!long.TryParse(expiry, NumberStyles.None, CultureInfo.InvariantCulture, out var millis)) { throw new GoogleInvalidResponseException(); }
        try { return new(HistoryId(root), DateTimeOffset.FromUnixTimeMilliseconds(millis)); }
        catch (ArgumentOutOfRangeException) { throw new GoogleInvalidResponseException(); }
    }

    public static GoogleMessagePage MessagePage(JsonElement root, int take)
        => new([.. Array(root, "messages", take).Select(value => new GoogleMessageReference(Required(value, "id"), Required(value, "threadId")))],
            Optional(root, "nextPageToken", 8192));

    public static GoogleMessageMetadata Metadata(JsonElement root)
    {
        var headers = root.TryGetProperty("payload", out var payload) ? Headers(payload) : [];
        var labels = Array(root, "labelIds", 256).Select(value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: <= 1024 } label
            ? label : throw new GoogleInvalidResponseException()).ToArray();
        DateTimeOffset? received = null;
        var date = Optional(root, "internalDate", 20);
        if (date is not null)
        {
            if (!long.TryParse(date, NumberStyles.None, CultureInfo.InvariantCulture, out var millis)) { throw new GoogleInvalidResponseException(); }
            try { received = DateTimeOffset.FromUnixTimeMilliseconds(millis); }
            catch (ArgumentOutOfRangeException) { throw new GoogleInvalidResponseException(); }
        }
        return new(Required(root, "id"), Required(root, "threadId"), labels, received,
            Header(headers, "Subject"), Header(headers, "From"), Header(headers, "To"), Header(headers, "Cc"), Optional(root, "snippet", 16384));
    }

    public static GoogleMessageContent Message(JsonElement root)
    {
        var metadata = Metadata(root);
        var bodies = new List<GoogleBodyPart>();
        var omissions = new List<GoogleBodyOmission>();
        var bytes = 0;
        var visited = 0;
        var truncated = false;
        var attachments = false;
        if (root.TryGetProperty("payload", out var payload)) { Visit(payload, 0); }
        return new(metadata, [.. bodies], bytes, truncated, attachments, [.. omissions]);

        void Visit(JsonElement part, int depth)
        {
            var id = Optional(part, "partId") ?? "";
            var type = Optional(part, "mimeType", 256);
            if (depth > 16 || ++visited > 256)
            {
                omissions.Add(new(id, type, "traversal_limit"));
                truncated = true;
                return;
            }
            var filename = Optional(part, "filename", 4096);
            if (!string.IsNullOrEmpty(filename) || type?.StartsWith("message/", StringComparison.OrdinalIgnoreCase) == true)
            {
                attachments = true;
                var reason = part.TryGetProperty("body", out var attachmentBody) && Optional(attachmentBody, "attachmentId") is not null
                    ? "attachment_data_not_fetched" : "unsupported_body_type";
                omissions.Add(new(id, type, reason));
                return;
            }
            if (part.TryGetProperty("body", out var body))
            {
                var external = Optional(body, "attachmentId");
                var data = Optional(body, "data", MaxResponseBytes);
                if (external is not null)
                {
                    attachments = true;
                    omissions.Add(new(id, type, "attachment_data_not_fetched"));
                    return;
                }
                else if (data is not null && (data.Length > 0 || type is "text/plain" or "text/html"))
                {
                    if (type is not ("text/plain" or "text/html") || !string.IsNullOrEmpty(filename))
                    {
                        attachments = true;
                        omissions.Add(new(id, type, "unsupported_body_type"));
                    }
                    else
                    {
                        byte[] decoded;
                        try
                        {
                            var unpadded = data.TrimEnd('=');
                            if (data.Length - unpadded.Length > 2 || unpadded.Any(value => !(char.IsAsciiLetterOrDigit(value) || value is '-' or '_')))
                            { throw new FormatException(); }
                            var base64 = unpadded.Replace('-', '+').Replace('_', '/');
                            decoded = Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
                        }
                        catch (FormatException) { throw new GoogleInvalidResponseException(); }
                        var contentType = Header(Headers(part), "Content-Type");
                        var charset = contentType is not null && ContentType.TryParse(contentType, out var parsed) ? parsed.Charset : null;
                        string text;
                        try
                        {
                            var encoding = string.IsNullOrWhiteSpace(charset) ? new UTF8Encoding(false, true)
                                : Encoding.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                            text = encoding.GetString(decoded);
                        }
                        catch (ArgumentException) { omissions.Add(new(id, type, "unsupported_body_type")); return; }
                        var builder = new StringBuilder();
                        foreach (var rune in text.EnumerateRunes())
                        {
                            if (bytes + rune.Utf8SequenceLength > MaxBodyBytes)
                            {
                                omissions.Add(new(id, type, "size_limit"));
                                truncated = true;
                                break;
                            }
                            builder.Append(rune);
                            bytes += rune.Utf8SequenceLength;
                        }
                        bodies.Add(new(type, builder.ToString()));
                    }
                }
            }
            if (!part.TryGetProperty("parts", out var children)) { return; }
            if (children.ValueKind != JsonValueKind.Array) { throw new GoogleInvalidResponseException(); }
            foreach (var child in children.EnumerateArray())
            {
                if (visited >= 256)
                {
                    omissions.Add(new(id, type, "traversal_limit"));
                    truncated = true;
                    break;
                }
                Visit(child, depth + 1);
            }
        }
    }

    private static JsonElement[] Array(JsonElement root, string property, int maximum)
    {
        if (root.ValueKind != JsonValueKind.Object) { throw new GoogleInvalidResponseException(); }
        if (!root.TryGetProperty(property, out var value)) { return []; }
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximum) { throw new GoogleInvalidResponseException(); }
        return [.. value.EnumerateArray()];
    }

    private static KeyValuePair<string, string>[] Headers(JsonElement part)
        => [.. Array(part, "headers", 256).Select(header => new KeyValuePair<string, string>(Required(header, "name", 256), Optional(header, "value", 16384) ?? ""))];

    private static string? Header(KeyValuePair<string, string>[] headers, string name)
        => headers.FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
