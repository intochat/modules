using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// MachineIdFile empty = %LOCALAPPDATA%\IntoChat.OS\machine-id; tests point it at a temp file.
public sealed class WindowsOptions : IModuleOptions
{
    public string MachineIdFile { get; set; } = "";

    public void Validate() { }
}
