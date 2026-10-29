using System.Diagnostics;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

internal sealed record ProcessResult(int ExitCode, string Output, string Error);

internal interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string file, string[] args, string workingDirectory, IReadOnlyDictionary<string, string>? environment, CancellationToken ct);
    // Starts a process that outlives the caller: it gets a console of its own, so the console signal that
    // stops the caller (Aspire stopping the silo) never reaches it. Answers the process id.
    int Detach(string file, string[] args, string workingDirectory);
}

internal sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string file, string[] args, string workingDirectory, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
    {
        using var process = new Process { StartInfo = Info(file, args, workingDirectory, environment, redirect: true) };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        try { await process.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            throw;
        }
        return new(process.ExitCode, await output, await error);
    }

    public int Detach(string file, string[] args, string workingDirectory)
    {
        var info = Info(file, args, workingDirectory, null, redirect: false);
        info.CreateNoWindow = true;
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} did not start");
        return process.Id;
    }

    private static ProcessStartInfo Info(string file, string[] args, string workingDirectory, IReadOnlyDictionary<string, string>? environment, bool redirect)
    {
        var info = new ProcessStartInfo(file) { WorkingDirectory = workingDirectory, RedirectStandardOutput = redirect, RedirectStandardError = redirect, UseShellExecute = false };
        foreach (var argument in args) { info.ArgumentList.Add(argument); }
        foreach (var (name, value) in environment ?? new Dictionary<string, string>()) { info.Environment[name] = value; }
        return info;
    }
}
