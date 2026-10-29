using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// Coordination belongs to one module host, not a process-wide cache of user paths. DI disposes it with that host.
internal sealed class WindowsStorageCoordinator(ILogger<WindowsStorageCoordinator>? logger = null) : IDisposable
{
    private static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    internal ConcurrentDictionary<string, SemaphoreSlim> Gates { get; } = new(Paths);
    internal ConcurrentDictionary<string, byte> WorkingFolders { get; } = new(Paths);
    internal ILogger Logger { get; } = logger ?? NullLogger<WindowsStorageCoordinator>.Instance;

    public void Dispose()
    {
        foreach (var gate in Gates.Values) { gate.Dispose(); }
        Gates.Clear();
        WorkingFolders.Clear();
    }
}
