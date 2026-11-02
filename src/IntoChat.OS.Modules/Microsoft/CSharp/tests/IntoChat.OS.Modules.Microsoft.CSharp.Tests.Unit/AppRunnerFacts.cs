using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DigitalBrain.Capabilities.Storage;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class AppRunnerFacts
{
    [Fact]
    public void ResolveCapability_ThrowsUnauthorizedAccessException_WhenCapabilityNotConsented()
    {
        var manifest = new AppManifest(
            Title: "UntrustedApp",
            Description: "An untrusted app without declared capabilities.",
            Operations: [],
            Settings: [],
            Slots: [],
            Requires: new AppRequirements(
                SemanticTypes: [],
                Connectors: [],
                Secrets: [],
                Capabilities: []));

        var snapshot = new AppSnapshot(
            Name: "untrusted",
            Manifest: manifest,
            Spec: "",
            Files: [],
            Settings: [],
            Connections: [],
            Version: "1.0.0",
            Status: AppStatus.Live,
            Activity: [],
            Versions: []);

        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            AppRunner.ResolveCapability(brain: null!, snapshot, typeof(IStorage)));

        Assert.Contains("Security Violation", ex.Message);
        Assert.Contains("UntrustedApp", ex.Message);
        Assert.Contains("storage@1", ex.Message);
    }

    [Fact]
    public async Task CachedAsync_ConcurrentCalls_SameHash_StressTest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "apprunner-stress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var testBytes = RandomNumberGenerator.GetBytes(4096);
            var hash = Convert.ToHexStringLower(SHA256.HashData(testBytes));
            var handler = new TestHttpMessageHandler(testBytes, delayMs: 15);
            using var http = new HttpClient(handler);
            var targetUri = new Uri("http://localhost/stress.dll");

            const int concurrency = 20;
            var tasks = Enumerable.Range(0, concurrency)
                .Select(_ => AppRunner.CachedAsync(http, tempDir, hash, targetUri, verify: true, TestContext.Current.CancellationToken))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(concurrency, results.Length);
            foreach (var result in results)
            {
                Assert.Equal(testBytes, result);
            }

            var cachedFilePath = Path.Combine(tempDir, hash);
            Assert.True(File.Exists(cachedFilePath));
            var fileBytes = await File.ReadAllBytesAsync(cachedFilePath, TestContext.Current.CancellationToken);
            Assert.Equal(testBytes, fileBytes);
            Assert.Equal(1, handler.CallCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort cleanup */ }
            }
        }
    }

    [Fact]
    public void AppLoadContext_IsCollectible_IsTrue()
    {
        var alc = new AppRunner.AppLoadContext("test-behavior");
        try
        {
            Assert.True(alc.IsCollectible);
            Assert.Equal("app test-behavior", alc.Name);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void AppLoadContext_LoadingAssembly_Unload_ReclaimedByGC()
    {
        var check = new CSharpScriptCheck();
        var result = check.Compile(new Dictionary<string, string>
        {
            ["App.cs"] = """
                [App("collectible-app")]
                public sealed class CollectibleApp : DigitalBrain.App
                {
                    public static void Run() => Console.WriteLine("running");
                }
                """
        });
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var assemblyBytes = result.Assemblies["App.cs"].Assembly;

        var weakRef = LoadAssemblyAndUnload(assemblyBytes);

        for (var i = 0; i < 20 && weakRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weakRef.IsAlive, "AssemblyLoadContext was not collected by GC after Unload().");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAssemblyAndUnload(byte[] assemblyBytes)
    {
        var alc = new AppRunner.AppLoadContext("test-gc");
        using var stream = new MemoryStream(assemblyBytes);
        var asm = alc.LoadFromStream(stream);
        var appType = AppRunner.AppTypeIn(asm);
        Assert.NotNull(appType);
        Assert.Equal("CollectibleApp", appType.Name);

        alc.Unload();
        return new WeakReference(alc, trackResurrection: true);
    }

    [Fact]
    public async Task AppRunner_DisposeAsync_UnloadsContext_And_ALC_IsCollectedByGC()
    {
        var check = new CSharpScriptCheck();
        var result = check.Compile(new Dictionary<string, string>
        {
            ["App.cs"] = """
                [App("runner-app")]
                public sealed class RunnerApp : DigitalBrain.App
                {
                    public static void Run() => Console.WriteLine("running");
                }
                """
        });
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var assemblyBytes = result.Assemblies["App.cs"].Assembly;

        var (weakAlc, weakRunner) = await CreateAndDisposeRunner(assemblyBytes);

        for (var i = 0; i < 20 && (weakAlc.IsAlive || weakRunner.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weakAlc.IsAlive, "AssemblyLoadContext was not collected by GC after AppRunner disposal.");
        Assert.False(weakRunner.IsAlive, "AppRunner was not collected by GC.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference WeakAlc, WeakReference WeakRunner)> CreateAndDisposeRunner(byte[] assemblyBytes)
    {
        var alc = new AppRunner.AppLoadContext("runner-behavior");
        using var stream = new MemoryStream(assemblyBytes);
        var asm = alc.LoadFromStream(stream);
        var appType = AppRunner.AppTypeIn(asm)!;

        var assignment = new AssignmentEvent(
            Behavior: "runner-behavior",
            Hash: "hash123",
            Assembly: new Uri("http://localhost/app.dll"),
            Symbols: new Uri("http://localhost/app.pdb"),
            Settings: new Dictionary<string, string>());

        var runner = new AppRunner(assignment, alc, appType, logLines: 50);
        Assert.NotNull(runner.Context);
        Assert.Same(alc, runner.Context);

        await runner.DisposeAsync();
        Assert.Null(runner.Context);

        return (new WeakReference(alc, trackResurrection: true), new WeakReference(runner, trackResurrection: true));
    }

    [Fact]
    public async Task AppRunner_DisposeAsync_IsIdempotent_SequentialAndConcurrent()
    {
        var assignment = new AssignmentEvent(
            Behavior: "test-behavior",
            Hash: "abc123hash",
            Assembly: new Uri("http://localhost/app.dll"),
            Symbols: new Uri("http://localhost/app.pdb"),
            Settings: new Dictionary<string, string>());

        var alc = new AppRunner.AppLoadContext("test-behavior");
        var runner = new AppRunner(assignment, alc, typeof(object), logLines: 50);

        Assert.NotNull(runner.Context);
        Assert.Same(alc, runner.Context);

        // 1. First disposal unloads context and cancels stopping token
        await runner.DisposeAsync();
        Assert.Null(runner.Context);

        // 2. 20 consecutive sequential calls execute idempotently without exception
        for (var i = 0; i < 20; i++)
        {
            await runner.DisposeAsync();
            Assert.Null(runner.Context);
        }

        // 3. 20 concurrent tasks calling DisposeAsync simultaneously all succeed
        var tasks = Enumerable.Range(0, 20).Select(_ => runner.DisposeAsync().AsTask()).ToArray();
        await Task.WhenAll(tasks);
        Assert.Null(runner.Context);
    }

    [Fact]
    public async Task CachedAsync_CancellationMidFlight_ReleasesGate_CleansTempFile_AndAllowsSubsequentDownload()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "apprunner-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var testBytes = RandomNumberGenerator.GetBytes(2048);
            var hash = Convert.ToHexStringLower(SHA256.HashData(testBytes));
            var targetUri = new Uri("http://localhost/cancel.dll");

            var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var handler = new BlockingTestHttpMessageHandler(testBytes, requestStarted, cancelTcs.Task);
            using var http = new HttpClient(handler);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

            // Start CachedAsync in background
            var backgroundTask = Task.Run(async () =>
            {
                await AppRunner.CachedAsync(http, tempDir, hash, targetUri, verify: true, cts.Token);
            }, TestContext.Current.CancellationToken);

            // Wait until the HTTP request has begun executing inside the gate
            await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Cancel mid-flight
            await cts.CancelAsync();
            cancelTcs.TrySetResult();

            // Assert that OperationCanceledException was thrown
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backgroundTask);

            // Assert no .tmp file leaked in tempDir
            var tmpFiles = Directory.GetFiles(tempDir, "*.tmp");
            Assert.Empty(tmpFiles);

            // Assert destination file was not created/corrupted
            var destinationFile = Path.Combine(tempDir, hash);
            Assert.False(File.Exists(destinationFile));

            // Assert gate was released: a subsequent call with a non-cancelled token must succeed immediately
            var immediateHandler = new TestHttpMessageHandler(testBytes);
            using var http2 = new HttpClient(immediateHandler);

            var downloadedBytes = await AppRunner.CachedAsync(http2, tempDir, hash, targetUri, verify: true, TestContext.Current.CancellationToken);
            Assert.Equal(testBytes, downloadedBytes);
            Assert.True(File.Exists(destinationFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort cleanup */ }
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_Cancellation_LeavesCleanDisk_AndNoHungGate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "apprunner-prep-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var asmBytes = RandomNumberGenerator.GetBytes(1024);
            var asmHash = Convert.ToHexStringLower(SHA256.HashData(asmBytes));
            var pdbBytes = RandomNumberGenerator.GetBytes(512);

            var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var handler = new BlockingTestHttpMessageHandler(asmBytes, requestStarted, cancelTcs.Task);
            using var http = new HttpClient(handler);

            var assignment = new AssignmentEvent(
                Behavior: "cancel-behavior",
                Hash: asmHash,
                Assembly: new Uri("http://localhost/app.dll"),
                Symbols: new Uri("http://localhost/app.pdb"),
                Settings: new Dictionary<string, string>());

            var options = Options.Create(new SandboxOptions { CacheRoot = tempDir });
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

            var prepTask = Task.Run(async () =>
            {
                await AppRunner.PrepareAsync(http, options, assignment, cts.Token);
            }, TestContext.Current.CancellationToken);

            await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await cts.CancelAsync();
            cancelTcs.TrySetResult();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => prepTask);

            // Gate must not be hung, disk must be clean of tmp files
            var tmpFiles = Directory.GetFiles(tempDir, "*.tmp");
            Assert.Empty(tmpFiles);

            // Subsequent PrepareAsync succeeds
            var successHandler = new MultiResourceHttpMessageHandler(new Dictionary<Uri, byte[]>
            {
                [assignment.Assembly] = asmBytes,
                [assignment.Symbols] = pdbBytes
            });
            using var httpSuccess = new HttpClient(successHandler);

            await AppRunner.PrepareAsync(httpSuccess, options, assignment, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(Path.Combine(tempDir, asmHash)));
            Assert.True(File.Exists(Path.Combine(tempDir, asmHash + ".pdb")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort cleanup */ }
            }
        }
    }

    [Fact]
    public async Task CachedAsync_CorruptedExistingFile_OverwrittenCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "apprunner-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var validBytes = RandomNumberGenerator.GetBytes(2048);
            var hash = Convert.ToHexStringLower(SHA256.HashData(validBytes));
            var targetUri = new Uri("http://localhost/app.dll");
            var destPath = Path.Combine(tempDir, hash);

            // Pre-seed with corrupted/invalid bytes
            var corruptBytes = RandomNumberGenerator.GetBytes(1024);
            await File.WriteAllBytesAsync(destPath, corruptBytes, TestContext.Current.CancellationToken);

            var handler = new TestHttpMessageHandler(validBytes);
            using var http = new HttpClient(handler);

            // CachedAsync with verify: true detects hash mismatch and redownloads/replaces
            var result = await AppRunner.CachedAsync(http, tempDir, hash, targetUri, verify: true, TestContext.Current.CancellationToken);

            Assert.Equal(validBytes, result);
            var onDisk = await File.ReadAllBytesAsync(destPath, TestContext.Current.CancellationToken);
            Assert.Equal(validBytes, onDisk);
            Assert.Equal(1, handler.CallCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort cleanup */ }
            }
        }
    }

    private sealed class TestHttpMessageHandler(byte[] content, int delayMs = 0) : HttpMessageHandler
    {
        private int _callCount;
        public int CallCount => _callCount;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
        }
    }

    private sealed class BlockingTestHttpMessageHandler(byte[] content, TaskCompletionSource started, Task release) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await release.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
        }
    }

    private sealed class MultiResourceHttpMessageHandler(IReadOnlyDictionary<Uri, byte[]> resources) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null && resources.TryGetValue(request.RequestUri, out var bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
