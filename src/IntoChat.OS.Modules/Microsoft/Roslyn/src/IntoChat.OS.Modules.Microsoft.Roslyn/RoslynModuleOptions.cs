using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed class RoslynModuleOptions : IModuleOptions
{
    public string? SolutionPath { get; set; }
    public string WorkspaceKey { get; set; } = "intochat-os";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceKey))
        { throw new InvalidOperationException("Roslyn workspace key must not be empty."); }
    }
}
