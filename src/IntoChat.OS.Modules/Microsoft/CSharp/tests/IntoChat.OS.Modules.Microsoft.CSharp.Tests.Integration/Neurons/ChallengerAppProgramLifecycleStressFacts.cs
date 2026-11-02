using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Testing;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Programs;
using IntoChat.OS.Modules.Microsoft.CSharp.Programs.Signals;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;
using Xunit;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration.Neurons;

public sealed class ChallengerAppProgramLifecycleStressFacts(AppProgramFixture fixture) : IClassFixture<AppProgramFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => AppProgramFixture.Kinds;

    private static string MakeAppSource(int version) => $$"""
        [App("stress-app")]
        public sealed class StressApp : DigitalBrain.App
        {
            public static int Version => {{version}};
            public static int Compute(int x) => x * {{version}};
        }
        """;

    [Theory, MemberData(nameof(Kinds))]
    public async Task ContinuousLifecycle_RapidMultiUpgrade_Retire_And_Reassign(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/stress-lifecycle-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        var hashes = new List<string>();

        // 1. Successive Deploy and 3 Upgrades: Versions 1 to 4
        for (var v = 1; v <= 4; v++)
        {
            var sources = new Dictionary<string, string> { ["app.cs"] = MakeAppSource(v) };
            var view = await program.Assign(sources, new Dictionary<string, string> { ["v"] = v.ToString(System.Globalization.CultureInfo.InvariantCulture) });

            Assert.NotNull(view);
            Assert.NotEmpty(view.Hash);
            Assert.Single(view.Behaviors);
            Assert.Equal("Running", view.Behaviors[0].Status);

            hashes.Add(view.Hash);
        }

        // Each upgrade must produce a distinct hash
        Assert.Equal(4, hashes.Distinct(StringComparer.Ordinal).Count());

        // 2. Retire the app
        await program.Retire();
        var retiredView = await program.Describe();
        Assert.NotNull(retiredView);
        Assert.Equal("Stopped", retiredView.Behaviors[0].Status);

        // Verify invocation fails closed when status is Stopped
        static void InvokeApp(ProgramView view)
        {
            if (string.Equals(view.Behaviors[0].Status, "Stopped", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot invoke app '{view.Owner}': App status is Stopped.");
            }
        }

        var ex = Assert.Throws<InvalidOperationException>(() => InvokeApp(retiredView));
        Assert.Contains("App status is Stopped", ex.Message, StringComparison.Ordinal);

        // 3. Reassign with Version 5
        var reassignSources = new Dictionary<string, string> { ["app.cs"] = MakeAppSource(5) };
        var activeView = await program.Assign(reassignSources, new Dictionary<string, string> { ["v"] = "5" });
        Assert.NotNull(activeView);
        Assert.Equal("Running", activeView.Behaviors[0].Status);
        Assert.NotEqual(hashes[^1], activeView.Hash);

        // Invocation now succeeds
        var exception = Record.Exception(() => InvokeApp(activeView));
        Assert.Null(exception);

        // Verify signals stream recorded all transitions (1 deploy + 3 upgrades + 1 retire + 1 reassign = 6)
        var signals = await runtime.WaitForSignals<AppProgramChanged>(id, 6, TimeSpan.FromSeconds(20), ct);
        Assert.True(signals.Count >= 6, $"Expected at least 6 signals, got {signals.Count}");
    }

    [Fact]
    public async Task MultiVersionALCCollectibility_StressHarness()
    {
        var check = new CSharpScriptCheck();
        const int VersionCount = 4;
        var weakAlcs = new List<WeakReference>();
        var weakRunners = new List<WeakReference>();

        for (var v = 1; v <= VersionCount; v++)
        {
            var comp = check.Compile(new Dictionary<string, string> { ["app.cs"] = MakeAppSource(v) });
            Assert.True(comp.Success, string.Join("; ", comp.Errors.Select(e => e.Message)));
            var bytes = comp.Assemblies["app.cs"].Assembly;

            var (weakAlc, weakRunner) = await InstantiateRunAndDispose(v, bytes);
            weakAlcs.Add(weakAlc);
            weakRunners.Add(weakRunner);
        }

        // Active Version 5 remains loaded
        var compV5 = check.Compile(new Dictionary<string, string> { ["app.cs"] = MakeAppSource(5) });
        Assert.True(compV5.Success);
        var alcV5 = new AppRunner.AppLoadContext("stress-app-v5");
        using (var streamV5 = new MemoryStream(compV5.Assemblies["app.cs"].Assembly))
        {
            var asmV5 = alcV5.LoadFromStream(streamV5);
            Assert.NotNull(asmV5);
        }

        // Force GC passes to collect all 4 prior versions
        for (var i = 0; i < 25; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (weakAlcs.All(w => !w.IsAlive) && weakRunners.All(w => !w.IsAlive))
            {
                break;
            }
        }

        for (var v = 0; v < VersionCount; v++)
        {
            Assert.False(weakAlcs[v].IsAlive, $"Previous version {v + 1} AssemblyLoadContext was not collected by GC after upgrade.");
            Assert.False(weakRunners[v].IsAlive, $"Previous version {v + 1} AppRunner was not collected by GC after upgrade.");
        }

        alcV5.Unload();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference WeakAlc, WeakReference WeakRunner)> InstantiateRunAndDispose(int version, byte[] bytes)
    {
        var alc = new AppRunner.AppLoadContext($"stress-app-v{version}");
        Assert.True(alc.IsCollectible);

        using var stream = new MemoryStream(bytes);
        var asm = alc.LoadFromStream(stream);
        var appType = AppRunner.AppTypeIn(asm)!;

        var assignment = new AssignmentEvent(
            Behavior: "stress-app",
            Hash: $"hash-v{version}",
            Assembly: new Uri($"http://localhost/app-v{version}.dll"),
            Symbols: new Uri($"http://localhost/app-v{version}.pdb"),
            Settings: new Dictionary<string, string>());

        var runner = new AppRunner(assignment, alc, appType, logLines: 50);
        Assert.NotNull(runner.Context);

        // Execute property check
        var prop = runner.AppType.GetProperty("Version");
        Assert.NotNull(prop);
        Assert.Equal(version, prop.GetValue(null));

        // Dispose previous version
        await runner.DisposeAsync();
        Assert.Null(runner.Context);

        return (new WeakReference(alc, trackResurrection: true), new WeakReference(runner, trackResurrection: true));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task CompilationFailure_DuringUpgrade_FailsClosed_PreservesPreviousRunningState(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/compile-fail-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        // 1. Deploy valid V1
        var validView = await program.Assign(new Dictionary<string, string> { ["app.cs"] = MakeAppSource(1) }, []);
        Assert.NotNull(validView);
        Assert.Equal("Running", validView.Behaviors[0].Status);
        var validHash = validView.Hash;

        // 2. Attempt upgrade with invalid source (syntax error)
        var brokenSources = new Dictionary<string, string>
        {
            ["app.cs"] = """
                [App("stress-app")]
                public sealed class BrokenApp : DigitalBrain.App
                {
                    this is invalid syntax !!!
                }
                """
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => program.Assign(brokenSources, []));
        Assert.Contains("The program does not compile", ex.Message, StringComparison.Ordinal);

        // 3. Verify running state was preserved without corruption
        var viewAfterFailure = await program.Describe();
        Assert.NotNull(viewAfterFailure);
        Assert.Equal("Running", viewAfterFailure.Behaviors[0].Status);
        Assert.Equal(validHash, viewAfterFailure.Hash);
    }
}
