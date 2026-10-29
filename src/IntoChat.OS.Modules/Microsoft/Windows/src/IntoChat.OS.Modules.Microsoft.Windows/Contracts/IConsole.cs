using System.ComponentModel;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// cmd.exe on one Windows PC, keyed {connectionKey}/console; the working directory must be inside an allowed root.
// A plain neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("windows.console"), DigitalBrain.Contracts.DefaultNeuronType("windows.console")]
public interface IConsole
{
    [Description("Run one cmd.exe command line on this Windows machine and return its exit code, standard output and standard error. workingDirectory defaults to the first allowed root.")]
    [DigitalBrain.Interleaves, DigitalBrain.CallTimeout(300_000)]
    Task<CommandResult> Run(string command, string? workingDirectory = null, int timeoutSeconds = 60, CancellationToken cancellationToken = default);
}

[GenerateSerializer, Alias("windows.command-result")]
public sealed record CommandResult(
    [property: Id(0)] int ExitCode,
    [property: Id(1)] string StandardOutput,
    [property: Id(2)] string StandardError);
