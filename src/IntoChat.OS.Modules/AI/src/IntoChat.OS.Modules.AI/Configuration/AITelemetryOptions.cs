using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.AI;

public sealed class AITelemetryOptions
{
    public bool? EnableSensitiveData { get; set; }
}
