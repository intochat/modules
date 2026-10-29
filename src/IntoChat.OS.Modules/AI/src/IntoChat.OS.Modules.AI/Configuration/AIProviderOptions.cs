using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.AI;

public class AIProviderOptions
{
    // For key-based providers this only seeds the provider registration's Endpoint at startup.
    public string? Endpoint { get; set; }
}
