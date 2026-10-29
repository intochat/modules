using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.AI;

public sealed class AIDefaultOptions
{
    public string? Model { get; set; }
    public string? Reasoning { get; set; }
    public int? MaxOutputTokens { get; set; }
    public string? Embedding { get; set; }
    public string? Transcription { get; set; }
    public string? SystemOne { get; set; }
    public string? Image { get; set; }
}
