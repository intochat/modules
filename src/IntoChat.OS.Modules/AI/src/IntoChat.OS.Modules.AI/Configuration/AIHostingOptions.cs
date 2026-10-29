using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.AI;

public sealed class AIHostingOptions
{
    public int? MaxConcurrentRequests { get; set; }
    public int? MaxExecutionSeconds { get; set; }
    public int? MaxOutputTokens { get; set; }
    public List<string> Llms { get; set; } = [];
    public List<string> Embeddings { get; set; } = [];
    public List<string> SystemOnes { get; set; } = [];
}
