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

public sealed class AppProgramLifecycleFacts(AppProgramFixture fixture) : IClassFixture<AppProgramFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => AppProgramFixture.Kinds;

    private const string V1Source = """
        [App("lifecycle-app")]
        public sealed class LifecycleApp : DigitalBrain.App
        {
            public static int Version => 1;
            public static void Run() => Console.WriteLine("Version 1");
        }
        """;

    private const string V2Source = """
        [App("lifecycle-app")]
        public sealed class LifecycleApp : DigitalBrain.App
        {
            public static int Version => 2;
            public static void Run() => Console.WriteLine("Version 2");
        }
        """;

    [Theory, MemberData(nameof(Kinds))]
    public async Task FullLifecycle_Deploy_Call_Upgrade_Retire_TransitionsCleanly(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/full-lifecycle-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        // 1. Deploy V1
        var v1Sources = new Dictionary<string, string> { ["app.cs"] = V1Source };
        var viewV1 = await program.Assign(v1Sources, new Dictionary<string, string> { ["Env"] = "v1" });

        Assert.NotNull(viewV1);
        Assert.NotEmpty(viewV1.Hash);
        Assert.Single(viewV1.Behaviors);
        Assert.Equal("Running", viewV1.Behaviors[0].Status);

        var describedV1 = await program.Describe();
        Assert.NotNull(describedV1);
        Assert.Equal("Running", describedV1.Behaviors[0].Status);

        // 2. Call Turn on V1
        var check = new CSharpScriptCheck();
        var compV1 = check.Compile(v1Sources);
        Assert.True(compV1.Success);
        var v1Bytes = compV1.Assemblies["app.cs"].Assembly;

        await using (var runnerV1 = CreateRunner("lifecycle-app", viewV1.Hash, v1Bytes))
        {
            Assert.NotNull(runnerV1.Context);
            var versionProp = runnerV1.AppType.GetProperty("Version");
            Assert.NotNull(versionProp);
            Assert.Equal(1, versionProp.GetValue(null));
        }

        // 3. Upgrade to V2
        var v2Sources = new Dictionary<string, string> { ["app.cs"] = V2Source };
        var viewV2 = await program.Assign(v2Sources, new Dictionary<string, string> { ["Env"] = "v2" });

        Assert.NotNull(viewV2);
        Assert.NotEqual(viewV1.Hash, viewV2.Hash);
        Assert.Equal("Running", viewV2.Behaviors[0].Status);

        var compV2 = check.Compile(v2Sources);
        Assert.True(compV2.Success);
        var v2Bytes = compV2.Assemblies["app.cs"].Assembly;

        await using (var runnerV2 = CreateRunner("lifecycle-app", viewV2.Hash, v2Bytes))
        {
            Assert.NotNull(runnerV2.Context);
            var versionProp = runnerV2.AppType.GetProperty("Version");
            Assert.NotNull(versionProp);
            Assert.Equal(2, versionProp.GetValue(null));
        }

        // 4. Retire
        await program.Retire();

        var describedRetired = await program.Describe();
        Assert.NotNull(describedRetired);
        Assert.Equal("Stopped", describedRetired.Behaviors[0].Status);

        var signals = await runtime.WaitForSignals<AppProgramChanged>(id, 3, TimeSpan.FromSeconds(20), ct);
        Assert.True(signals.Count >= 3, $"Expected at least 3 signals (Deploy, Upgrade, Retire), got {signals.Count}");
    }

    [Fact]
    public async Task AppUpgrade_UnloadsPreviousAssemblyLoadContext()
    {
        var check = new CSharpScriptCheck();

        var compV1 = check.Compile(new Dictionary<string, string> { ["app.cs"] = V1Source });
        Assert.True(compV1.Success);
        var v1Bytes = compV1.Assemblies["app.cs"].Assembly;

        var compV2 = check.Compile(new Dictionary<string, string> { ["app.cs"] = V2Source });
        Assert.True(compV2.Success);
        var v2Bytes = compV2.Assemblies["app.cs"].Assembly;

        // Load V1 and then dispose it as part of an upgrade
        var (weakAlcV1, weakRunnerV1) = await RunAndDisposeV1(v1Bytes);

        // V2 loads into a new independent context
        var alcV2 = new AppRunner.AppLoadContext("lifecycle-app-v2");
        using var streamV2 = new MemoryStream(v2Bytes);
        var asmV2 = alcV2.LoadFromStream(streamV2);
        Assert.NotNull(asmV2);

        // Force GC to collect the unloaded V1 context
        for (var i = 0; i < 20 && (weakAlcV1.IsAlive || weakRunnerV1.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weakAlcV1.IsAlive, "Previous version AssemblyLoadContext was not collected by GC after upgrade.");
        Assert.False(weakRunnerV1.IsAlive, "Previous version AppRunner was not collected by GC after upgrade.");

        // Clean up V2
        alcV2.Unload();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference WeakAlc, WeakReference WeakRunner)> RunAndDisposeV1(byte[] v1Bytes)
    {
        var alcV1 = new AppRunner.AppLoadContext("lifecycle-app-v1");
        Assert.True(alcV1.IsCollectible);

        using var stream = new MemoryStream(v1Bytes);
        var asm = alcV1.LoadFromStream(stream);
        var appType = AppRunner.AppTypeIn(asm)!;

        var assignment = new AssignmentEvent(
            Behavior: "lifecycle-app",
            Hash: "hash-v1",
            Assembly: new Uri("http://localhost/app-v1.dll"),
            Symbols: new Uri("http://localhost/app-v1.pdb"),
            Settings: new Dictionary<string, string>());

        var runner = new AppRunner(assignment, alcV1, appType, logLines: 50);
        Assert.NotNull(runner.Context);

        // Simulate app upgrade: dispose previous version runner and unload its ALC
        await runner.DisposeAsync();
        Assert.Null(runner.Context);

        return (new WeakReference(alcV1, trackResurrection: true), new WeakReference(runner, trackResurrection: true));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task RetiredApp_RejectsInvocations_UntilReassigned(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/reject-retired-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        // 1. Assign and verify running
        await program.Assign(new Dictionary<string, string> { ["app.cs"] = V1Source }, []);
        var activeView = await program.Describe();
        Assert.NotNull(activeView);
        Assert.Equal("Running", activeView.Behaviors[0].Status);

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
        Assert.Contains("App status is Stopped", ex.Message);

        // 3. Reassign app with new/updated code -> returns to Running
        var reassignedView = await program.Assign(new Dictionary<string, string> { ["app.cs"] = V2Source }, []);
        Assert.NotNull(reassignedView);
        Assert.Equal("Running", reassignedView.Behaviors[0].Status);

        // Invocations now succeed again
        var exception = Record.Exception(() => InvokeApp(reassignedView));
        Assert.Null(exception);
    }

    private static AppRunner CreateRunner(string behavior, string hash, byte[] assemblyBytes)
    {
        var alc = new AppRunner.AppLoadContext(behavior);
        using var stream = new MemoryStream(assemblyBytes);
        var asm = alc.LoadFromStream(stream);
        var appType = AppRunner.AppTypeIn(asm)!;

        var assignment = new AssignmentEvent(
            Behavior: behavior,
            Hash: hash,
            Assembly: new Uri($"http://localhost/{behavior}.dll"),
            Symbols: new Uri($"http://localhost/{behavior}.pdb"),
            Settings: new Dictionary<string, string>());

        return new AppRunner(assignment, alc, appType, logLines: 50);
    }
}
