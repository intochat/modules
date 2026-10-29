namespace IntoChat.OS.Modules.Google.Gmail;

internal interface IGmailApi
{
    Task<GoogleApiResult<GoogleProfile>> ProfileAsync(string accessToken, GoogleCallContext context, CancellationToken ct);
    Task<GoogleApiResult<GoogleMessagePage>> ListMessagesAsync(string accessToken, GoogleCallContext context, int take, string? pageToken, CancellationToken ct);
    Task<GoogleApiResult<GoogleMessageMetadata>> ReadMetadataAsync(string accessToken, GoogleCallContext context, string messageId, CancellationToken ct);
    Task<GoogleApiResult<GoogleMessageContent>> ReadMessageAsync(string accessToken, GoogleCallContext context, string messageId, CancellationToken ct);
    Task<GoogleApiResult<GoogleWatch>> WatchAsync(string accessToken, GoogleCallContext context, string topic, CancellationToken ct);
    Task<GoogleApiResult<GoogleUnit>> StopAsync(string accessToken, GoogleCallContext context, CancellationToken ct);
    Task<GoogleApiResult<GoogleHistoryPage>> HistoryAsync(string accessToken, GoogleCallContext context, string startHistoryId, string? pageToken, int take, CancellationToken ct);
    Task<GoogleDraftWriteResult> CreateDraftAsync(string accessToken, GoogleCallContext context, byte[] mime, CancellationToken ct);
}
