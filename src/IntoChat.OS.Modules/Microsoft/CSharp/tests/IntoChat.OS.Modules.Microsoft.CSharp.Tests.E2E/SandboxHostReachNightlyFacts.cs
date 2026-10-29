using System.Diagnostics;
using System.Net;
using System.Xml.Linq;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.E2E;

// The local profile runs the C# sandbox with only a host mapping (--add-host host.docker.internal:host-gateway) and a named
// volume. This builds the shipped Dockerfile and proves that container comes up healthy and resolves the host name sessions
// dial back to the brain on. Nightly: it needs a Docker engine and a cold image build.
public sealed class SandboxHostReachNightlyFacts
{
    private const string SandboxProject = "IntoChat.OS.Modules.Microsoft.CSharp.Sandbox";
    private const string Image = "intochat-os-sandbox-reach-fact:local";

    [Trait("tier", "nightly")]
    [Fact]
    public async Task TheSandboxBecomesHealthyAndResolvesTheHostName()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = RepositoryRoot();
        var container = "sandbox-reach-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            Assert.Equal(0, (await Docker(["build", "-f", DockerfileOf(root), "-t", Image, root], ct)).ExitCode);
            var run = await Docker(["run", "-d", "--name", container, "--add-host", "host.docker.internal:host-gateway",
                "-v", container + "-cache:/cache", "-p", "127.0.0.1::8080", Image], ct);
            Assert.Equal(0, run.ExitCode);

            var port = (await Docker(["port", container, "8080/tcp"], ct)).Output.Trim().Split(':').Last();
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            var healthy = false;
            for (var attempt = 0; attempt < 60 && !healthy; attempt++)
            {
                try { healthy = (await http.GetAsync("/health", ct)).StatusCode == HttpStatusCode.OK; }
                catch (HttpRequestException) { }
                if (!healthy) { await Task.Delay(TimeSpan.FromSeconds(1), ct); }
            }
            Assert.True(healthy, "The sandbox did not answer /health with 200 within a minute.");

            var resolved = await Docker(["exec", container, "getent", "hosts", "host.docker.internal"], ct);
            Assert.Equal(0, resolved.ExitCode);
            Assert.Contains("host.docker.internal", resolved.Output);
        }
        finally
        {
            await Docker(["rm", "-f", "-v", container], CancellationToken.None);
            await Docker(["volume", "rm", "-f", container + "-cache"], CancellationToken.None);
        }
    }

    // The sandbox project's directory comes from the solution map, never from a path literal.
    private static string DockerfileOf(string root)
    {
        var project = XDocument.Load(Path.Combine(root, "Intochat.OS.slnx")).Descendants("Project")
            .Select(entry => entry.Attribute("Path")!.Value.Replace('\\', '/'))
            .Single(path => path.EndsWith("/" + SandboxProject + ".csproj", StringComparison.Ordinal));
        return Path.Combine(root, Path.GetDirectoryName(project)!, "Dockerfile");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Intochat.OS.slnx"))) { return directory.FullName; }
        }
        throw new InvalidOperationException("Intochat.OS.slnx was not found above the test binary.");
    }

    private static async Task<(int ExitCode, string Output)> Docker(string[] arguments, CancellationToken ct)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("docker did not start.");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await output + await error);
    }
}
