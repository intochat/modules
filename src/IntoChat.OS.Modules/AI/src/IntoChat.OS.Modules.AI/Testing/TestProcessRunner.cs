using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntoChat.OS.Framework.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.AI.Testing;

internal static class TestProcessRunner
{
    private static readonly string[] StripEnvironmentVariables =
    [
        "MSBuildSDKsPath",
        "MSBUILD_EXE_PATH",
        "MSBuildExtensionsPath",
        "DOTNET_HOST_PATH",
        "TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"
    ];

    public static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.EnumerateFiles(dir.FullName, "*.slnx").Any())
            {
                return dir.FullName;
            }
        }
        return Directory.GetCurrentDirectory();
    }

    public static string? FindExecutable(string repoRoot, string suiteName, string? configuration)
    {
        var name = suiteName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || suiteName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || suiteName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(suiteName)
            : Path.GetFileName(suiteName);
        string[] configs = configuration is not null
            ? [configuration, configuration.ToLowerInvariant(), configuration.ToUpperInvariant()]
            : ["debug", "Debug", "release", "Release"];

        foreach (var cfg in configs)
        {
            var binaryName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
            var exePath = Path.Combine(repoRoot, "artifacts", "bin", name, cfg, binaryName);
            if (File.Exists(exePath))
            {
                return exePath;
            }
        }
        return null;
    }

    internal static Func<TestExecutionRequest, CancellationToken, Task<TestExecutionResult>>? OverrideExecutor { get; set; }

    public static async Task<TestExecutionResult> ExecuteAsync(TestExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (OverrideExecutor is not null)
        {
            return await OverrideExecutor(request, cancellationToken);
        }

        var repoRoot = FindRepositoryRoot();
        var tempDir = Path.Combine(Path.GetTempPath(), "intochat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var exePath = FindExecutable(repoRoot, request.SuiteName, request.Configuration);
        var startInfo = new ProcessStartInfo();

        foreach (var name in StripEnvironmentVariables)
        {
            startInfo.Environment.Remove(name);
        }
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        var args = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.Filter))
        {
            var escapedFilter = request.Filter.Replace("\"", "\\\"");
            args.Append($"--filter \"{escapedFilter}\" ");
        }

        if (exePath is not null)
        {
            startInfo.FileName = exePath;
            args.Append($"--report-xunit-trx --results-directory \"{tempDir}\" --no-ansi");
            startInfo.Arguments = args.ToString();
        }
        else
        {
            startInfo.FileName = "dotnet";
            var config = request.Configuration ?? "Release";
            var target = request.SuiteName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                ? request.SuiteName
                : $"{request.SuiteName}.csproj";
            args.Append($"--report-xunit-trx --results-directory \"{tempDir}\" --no-ansi");
            startInfo.Arguments = $"test \"{target}\" -c {config} --no-build {args}";
        }

        startInfo.WorkingDirectory = repoRoot;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        var timeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 120;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var watch = Stopwatch.StartNew();
        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process is null)
            {
                return new TestExecutionResult(
                    Success: false,
                    Total: 0,
                    Passed: 0,
                    Failed: 1,
                    Skipped: 0,
                    DurationMs: 0,
                    Summary: "Failed to start test runner process.",
                    Failures: [new TestFailureDetail(request.SuiteName, "Process.Start returned null")]);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(linkedCts.Token);
            watch.Stop();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var trxFiles = Directory.GetFiles(tempDir, "*.trx");
            if (trxFiles.Length > 0)
            {
                var result = TrxReportParser.Parse(trxFiles[0], watch.ElapsedMilliseconds);
                return result;
            }

            return new TestExecutionResult(
                Success: process.ExitCode == 0,
                Total: 0,
                Passed: 0,
                Failed: process.ExitCode == 0 ? 0 : 1,
                Skipped: 0,
                DurationMs: watch.ElapsedMilliseconds,
                Summary: $"Process exited with code {process.ExitCode} without producing a TRX report.",
                Failures: process.ExitCode == 0 ? [] : [new TestFailureDetail(request.SuiteName, $"ExitCode {process.ExitCode}:\n{stderr}\n{stdout}")]);
        }
        catch (OperationCanceledException)
        {
            watch.Stop();
            if (process is not null && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception killError) { NullLogger.Instance.LogSwallowed(killError, "Process tree termination on cancellation"); }
            }

            return new TestExecutionResult(
                Success: false,
                Total: 0,
                Passed: 0,
                Failed: 1,
                Skipped: 0,
                DurationMs: watch.ElapsedMilliseconds,
                Summary: $"Test execution timed out or was cancelled after {timeoutSeconds}s.",
                Failures: [new TestFailureDetail(request.SuiteName, "Test execution was cancelled or exceeded timeout.")]);
        }
        finally
        {
            process?.Dispose();
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception cleanupError)
            {
                NullLogger.Instance.LogSwallowed(cleanupError, "Temp directory cleanup");
            }
        }
    }
}
