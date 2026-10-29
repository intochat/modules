using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.AI;

// IOptions<AIOptions> is bound from the module manifest settings; this is the one direct read for
// construction-time consumers. Ollama model markers bind natively under Options:Ollama:Models.
internal static class AIOptionsRuntime
{
    internal static AIOptions Read(IConfiguration configuration)
        => configuration.GetModuleOptions<AIOptions>("ai");
}
