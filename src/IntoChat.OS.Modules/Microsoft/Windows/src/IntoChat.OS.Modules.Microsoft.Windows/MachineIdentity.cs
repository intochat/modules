using IntoChat.OS.Framework.Machine;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// The install's stable id: created once, then read, at the same %LOCALAPPDATA%\IntoChatOS\machine-id default and
// WindowsOptions.MachineIdFile option as before the runtime contract. A garbled file is an error, never silently
// replaced, because replacing it would orphan the machine's connection.
internal sealed class MachineIdentity(IOptions<WindowsOptions> options) : IMachineIdentity
{
    private readonly Lazy<string> _id = new(() => MachineIdFile.ReadOrCreate(MachineIdFile.Resolve(options.Value.MachineIdFile)));

    public string File { get; } = MachineIdFile.Resolve(options.Value.MachineIdFile);

    public string Id => _id.Value;

    public string DisplayName => Environment.MachineName;
}
