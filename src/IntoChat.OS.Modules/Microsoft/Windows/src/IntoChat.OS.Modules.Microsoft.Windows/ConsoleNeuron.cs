using IntoChat.OS.Framework.Machine;
using IntoChat.OS.Runtime.Storage.Confinement;
using System.Diagnostics;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Connections;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// A plain neuron (06-08) keyed {connectionKey}/console.
[DigitalBrain.Neuron("windows.console")]
internal sealed class ConsoleNeuron(IMachineIdentity identity) : DigitalBrain.Neuron, IConsole
{
    private const int MaxOutputChars = 20_000;

    // IConsole.Run's ResponseTimeout is five minutes; the command must end before the call does.
    private const int MaxTimeoutSeconds = 270;

    public async Task<CommandResult> Run(string command, string? workingDirectory = null, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var connection = WindowsKeys.Connection($"{Id.Brain.Value}/{Id.Key}");
        if (ConnectionKeys.Parse(connection).ConnectionId != identity.Id)
        { throw new ServiceUnavailableException("This Windows machine is not this silo's machine."); }
        var roots = await SiloNeurons.Get<IWindows>(Context.Services, connection).Roots();
        var directory = PathConfinement.Resolve(roots, string.IsNullOrWhiteSpace(workingDirectory) ? (roots.Length > 0 ? roots[0] : ".") : workingDirectory);
        var seconds = Math.Clamp(timeoutSeconds, 1, MaxTimeoutSeconds);

        // cmd.exe parses its own command line, so the command goes through verbatim rather than argv-quoted.
        var start = new ProcessStartInfo("cmd.exe", "/c " + command)
        { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested) { throw; }
            throw new TimeoutException($"The command ran longer than {seconds} seconds.");
        }
        return new CommandResult(process.ExitCode, Clip(await output), Clip(await error));
    }

    // The process may have exited between the timeout and the kill.
    private static bool TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); return true; }
        catch (Exception kill) when (kill is InvalidOperationException or NotSupportedException) { return false; }
    }

    private static string Clip(string text) => text.Length <= MaxOutputChars ? text : text[..MaxOutputChars];
}
