using System.ComponentModel;

namespace IntoChat.OS.Modules.Microsoft.Windows.Signals;

// Published by IFileSystem.Write only: a file changed on disk by anything else signals nothing.
[DigitalBrain.Signal("windows.file-written")]
[Description("A file was written through this machine's IFileSystem: its full path and the bytes written.")]
public sealed record FileWritten(string Path, long Length) : DigitalBrain.Signal;
