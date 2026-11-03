using System.Text;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// The install's stable id: created once, then read, at the same %LOCALAPPDATA%\IntoChatOS\machine-id default and
// WindowsOptions.MachineIdFile option as before the runtime contract. A garbled file is an error, never silently
// replaced, because replacing it would orphan the machine's connection.
public sealed class MachineIdentity(IOptions<WindowsOptions> options)
{
    private readonly Lazy<string> _id = new(() => ReadOrCreate(Resolve(options.Value.MachineIdFile)));

    public string File { get; } = Resolve(options.Value.MachineIdFile);

    public string Id => _id.Value;

    public string DisplayName => Environment.MachineName;

    private static string Resolve(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) { return configured; }
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IntoChatOS", "machine-id");
        }
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS()) { return Path.Combine(home, "Library", "Application Support", "IntoChatOS", "machine-id"); }
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(string.IsNullOrWhiteSpace(data) ? Path.Combine(home, ".local", "share") : data, "intochat-os", "machine-id");
    }

    private static string ReadOrCreate(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        if (System.IO.File.Exists(file))
        {
            var text = System.IO.File.ReadAllText(file).Trim();
            if (!Guid.TryParseExact(text, "N", out _)) { throw new InvalidDataException($"'{file}' does not hold a machine id."); }
            return text;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var id = Guid.NewGuid().ToString("N");
        var streamOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) { streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
        using var stream = new FileStream(file, streamOptions);
        stream.Write(Encoding.UTF8.GetBytes(id));
        return id;
    }
}
