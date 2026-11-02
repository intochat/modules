using System.Reflection;
using System.Security.Cryptography;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;
using Xunit;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class CSharpScriptCheckAdversarialFacts
{
    private readonly CSharpScriptCheck _check = new();

    // -------------------------------------------------------------
    // 1. DB0030: Zero [App] classes
    // -------------------------------------------------------------

    [Fact]
    public void ZeroAppClasses_SingleHelperFile_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Helper.cs"] = "public static class Helper { public static int Value => 42; }"
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Equal("Helper.cs", err.File);
        Assert.Contains("exactly one class decorated with [App]", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ZeroAppClasses_MultipleFiles_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["File1.cs"] = "public class Foo { }",
            ["File2.cs"] = "public class Bar { }",
            ["File3.cs"] = "public interface IBaz { }"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Id == "DB0030");
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ZeroAppClasses_OnlyCommentsOrStringsMentioningApp_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["AppComment.cs"] = """
                // [App("fake")]
                /* [App("fake2")] */
                public class Fake { string s = "[App(\"fake3\")]"; }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ZeroAppClasses_ClassInheritsDigitalBrainAppWithoutAppAttribute_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["UndecoratedApp.cs"] = """
                public sealed class NakedApp : DigitalBrain.App
                {
                    public static void Run() => Console.WriteLine("naked");
                }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Equal("UndecoratedApp.cs", err.File);
        Assert.Contains("The application must declare exactly one class decorated with [App]", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void AppClassNotDerivingFromDigitalBrainApp_Behavior()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["NotDeriving.cs"] = """
                [App("not-deriving")]
                public sealed class NotDerivingApp
                {
                    public static void Run() => Console.WriteLine(1);
                }
                """
        });

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => $"{e.Id}: {e.Message}")));
        var entry = Assert.Single(result.Assemblies);
        Assert.Equal("NotDeriving.cs", entry.Key);
        var loaded = Assembly.Load(entry.Value.Assembly);
        Assert.Null(loaded.EntryPoint);
        Assert.Equal("NotDerivingApp", AppRunner.AppTypeIn(loaded)?.Name);
    }

    // -------------------------------------------------------------
    // 2. DB0030: Multiple [App] classes
    // -------------------------------------------------------------

    [Fact]
    public void MultipleAppClasses_SameFile_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["TwoApps.cs"] = """
                [App("first")]
                public sealed class FirstApp : DigitalBrain.App { }

                [App("second")]
                public sealed class SecondApp : DigitalBrain.App { }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Equal("TwoApps.cs", err.File);
        Assert.Contains("found 2: FirstApp, SecondApp", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void MultipleAppClasses_AcrossDifferentFiles_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Alpha.cs"] = """
                [App("alpha")]
                public sealed class AlphaApp : DigitalBrain.App { }
                """,
            ["Beta.cs"] = """
                [App("beta")]
                public sealed class BetaApp : DigitalBrain.App { }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Contains("found 2: AlphaApp, BetaApp", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void MultipleAppClasses_NestedClassWithAppAttribute_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Nested.cs"] = """
                [App("outer")]
                public sealed class OuterApp : DigitalBrain.App
                {
                    [App("inner")]
                    public sealed class InnerApp : DigitalBrain.App { }
                }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Contains("found 2: OuterApp, InnerApp", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ZeroAppClasses_EmptyStringContent_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Empty.cs"] = "   \n\r\t   "
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ThreeAppClasses_AcrossNamespaces_FailsWithDB0030NamingAllThree()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["File1.cs"] = """
                namespace Ns1 {
                    [App("app1")]
                    public sealed class App1 : DigitalBrain.App { }
                }
                """,
            ["File2.cs"] = """
                namespace Ns2 {
                    [App("app2")]
                    public sealed class App2 : DigitalBrain.App { }
                }
                """,
            ["File3.cs"] = """
                namespace Ns3.Sub {
                    [App("app3")]
                    public sealed class App3 : DigitalBrain.App { }
                }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Contains("found 3: App1, App2, App3", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void SingleAppClass_DeeplyNestedNamespace_Succeeds()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Deep.cs"] = """
                namespace Level1.Level2.Level3.Level4 {
                    [App("deep-app", Accent = "#123456", Category = "Workspace")]
                    public sealed class DeepApp : DigitalBrain.App { }
                }
                """
        });

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => $"{e.Id}: {e.Message}")));
        var entry = Assert.Single(result.Assemblies);
        var loaded = Assembly.Load(entry.Value.Assembly);
        Assert.Null(loaded.EntryPoint);
        Assert.Equal("DeepApp", AppRunner.AppTypeIn(loaded)?.Name);
    }

    [Fact]
    public void SyntaxErrorInSource_FailsWithCompilationErrorNotDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Broken.cs"] = """
                [App("broken")]
                public sealed class BrokenApp : DigitalBrain.App
                {
                    public void Method( { }
                }
                """
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Id.StartsWith("CS"));
        Assert.DoesNotContain(result.Errors, e => e.Id == "DB0030");
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void AbstractAppClass_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["AbstractApp.cs"] = """
                [App("abstract-app")]
                public abstract class BaseApp : DigitalBrain.App { }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Equal("AbstractApp.cs", err.File);
        Assert.Contains("cannot be abstract or generic", err.Message);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void GenericAppClass_FailsWithDB0030()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["GenericApp.cs"] = """
                [App("generic-app")]
                public sealed class GenericApp<T> : DigitalBrain.App { }
                """
        });

        Assert.False(result.Success);
        var err = Assert.Single(result.Errors);
        Assert.Equal("DB0030", err.Id);
        Assert.Equal("GenericApp.cs", err.File);
        Assert.Contains("cannot be abstract or generic", err.Message);
        Assert.Empty(result.Assemblies);
    }

    // -------------------------------------------------------------
    // 4. Struct or Interface with [App]
    // -------------------------------------------------------------

    [Fact]
    public void StructDecoratedWithApp_FailsCompilation()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["StructApp.cs"] = """
                [App("struct-app")]
                public struct StructApp { }
                """
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Id is "CS0592" or "DB0030");
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void InterfaceDecoratedWithApp_FailsCompilation()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["InterfaceApp.cs"] = """
                [App("interface-app")]
                public interface IInterfaceApp { }
                """
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Id is "CS0592" or "DB0030");
        Assert.Empty(result.Assemblies);
    }

    // -------------------------------------------------------------
    // 5. Single DLL Invariants
    // -------------------------------------------------------------

    [Fact]
    public void SingleDLLInvariant_EntryPointIsNull_AndSingleAssemblyEmitted()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["synapses/calc.cs"] = """
                [App("calc")]
                public sealed class CalcApp : DigitalBrain.App
                {
                    public static int Add(int a, int b) => a + b;
                }
                """
        });

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => $"{e.Id}: {e.Message}")));
        Assert.Empty(result.Errors);

        // Dictionary contains exactly 1 assembly keyed by the app file path
        var entry = Assert.Single(result.Assemblies);
        Assert.Equal("synapses/calc.cs", entry.Key);

        var compiled = entry.Value;
        Assert.NotEmpty(compiled.Assembly);
        Assert.NotEmpty(compiled.Symbols);

        // Assembly hash is deterministic sha256
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(compiled.Assembly));
        Assert.Equal(expectedHash, compiled.Hash);

        // Load assembly and verify Single DLL Invariant: EntryPoint == null
        var loaded = Assembly.Load(compiled.Assembly);
        Assert.Null(loaded.EntryPoint);

        // AppRunner can locate the App type
        var appType = AppRunner.AppTypeIn(loaded);
        Assert.NotNull(appType);
        Assert.Equal("CalcApp", appType.Name);
    }

    // -------------------------------------------------------------
    // 6. Multi-file compilation into single assembly
    // -------------------------------------------------------------

    [Fact]
    public void MultiFileCompilation_FiveFiles_CompileIntoSingleAssembly()
    {
        var files = new Dictionary<string, string>
        {
            ["Models/Order.cs"] = """
                namespace MyApp.Models;
                public sealed record Order(string Id, decimal Amount);
                """,
            ["Services/IOrderService.cs"] = """
                namespace MyApp.Services;
                using MyApp.Models;
                public interface IOrderService { decimal CalculateTax(Order order); }
                """,
            ["Services/StandardOrderService.cs"] = """
                namespace MyApp.Services;
                using MyApp.Models;
                public sealed class StandardOrderService : IOrderService
                {
                    public decimal CalculateTax(Order order) => order.Amount * 0.2m;
                }
                """,
            ["Apps/OrderApp.Part1.cs"] = """
                namespace MyApp.Apps;
                using MyApp.Services;
                [App("order-app")]
                public sealed partial class OrderApp : DigitalBrain.App
                {
                    private readonly IOrderService _service = new StandardOrderService();
                }
                """,
            ["Apps/OrderApp.Part2.cs"] = """
                namespace MyApp.Apps;
                using MyApp.Models;
                public sealed partial class OrderApp
                {
                    public decimal Process(Order order) => order.Amount + _service.CalculateTax(order);
                }
                """
        };

        var result = _check.Compile(files);
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => $"{e.Id}: {e.Message}")));

        // Single assembly emitted, keyed by the app class file
        var entry = Assert.Single(result.Assemblies);
        Assert.Equal("Apps/OrderApp.Part1.cs", entry.Key);

        var loaded = Assembly.Load(entry.Value.Assembly);
        Assert.Null(loaded.EntryPoint);

        // Verify types from all 5 files exist in the single compiled assembly
        Assert.NotNull(loaded.GetType("MyApp.Models.Order"));
        Assert.NotNull(loaded.GetType("MyApp.Services.IOrderService"));
        Assert.NotNull(loaded.GetType("MyApp.Services.StandardOrderService"));
        var appType = loaded.GetType("MyApp.Apps.OrderApp");
        Assert.NotNull(appType);
        Assert.Same(appType, AppRunner.AppTypeIn(loaded));

        // Verify partial class methods both exist on OrderApp
        Assert.NotNull(appType.GetMethod("Process"));
    }

    [Fact]
    public void MultiFileCompilation_PartialClassBothDecoratedWithApp_FailsWithDuplicateAttributeCS0579()
    {
        var files = new Dictionary<string, string>
        {
            ["Part1.cs"] = """
                [App("my-app")]
                public sealed partial class MyApp : DigitalBrain.App { }
                """,
            ["Part2.cs"] = """
                [App("my-app")]
                public sealed partial class MyApp { }
                """
        };

        var result = _check.Compile(files);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Id == "CS0579");
    }

    [Fact]
    public void MultiFileCompilation_PartialClassDecoratedOnlyOnMainDeclaration_Succeeds()
    {
        var files = new Dictionary<string, string>
        {
            ["Main.cs"] = """
                [App("my-app")]
                public sealed partial class MyApp : DigitalBrain.App { }
                """,
            ["Extension.cs"] = """
                public sealed partial class MyApp
                {
                    public void Extra() { }
                }
                """
        };

        var result = _check.Compile(files);
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => $"{e.Id}: {e.Message}")));
        var entry = Assert.Single(result.Assemblies);
        Assert.True(files.ContainsKey(entry.Key));
        var loaded = Assembly.Load(entry.Value.Assembly);
        Assert.Null(loaded.EntryPoint);
        Assert.Equal("MyApp", AppRunner.AppTypeIn(loaded)?.Name);
    }

    // -------------------------------------------------------------
    // 7. Caching and Determinism Invariant
    // -------------------------------------------------------------

    [Fact]
    public void CachingInvariant_IdenticalInputsProduceCachedAssemblyWithoutReEmit()
    {
        var files = new Dictionary<string, string>
        {
            ["app.cs"] = """
                [App("cached-app")]
                public sealed class CachedApp : DigitalBrain.App
                {
                    public static int Get() => 123;
                }
                """
        };

        var initialEmits = _check.Emits;
        var result1 = _check.Compile(files);
        Assert.True(result1.Success);
        var emitsAfterFirst = _check.Emits;
        Assert.Equal(initialEmits + 1, emitsAfterFirst);

        // Second compile with exact same files must hit cache
        var result2 = _check.Compile(files);
        Assert.True(result2.Success);
        Assert.Equal(emitsAfterFirst, _check.Emits); // No new emit
        Assert.Equal(result1.Assemblies["app.cs"].Hash, result2.Assemblies["app.cs"].Hash);
        Assert.Same(result1.Assemblies["app.cs"], result2.Assemblies["app.cs"]);
    }

    [Fact]
    public void CachingInvariant_FileContentChangeCausesReEmitAndNewHash()
    {
        var files1 = new Dictionary<string, string>
        {
            ["app.cs"] = """
                [App("changing-app")]
                public sealed class ChangingApp : DigitalBrain.App { public int X = 1; }
                """
        };
        var files2 = new Dictionary<string, string>
        {
            ["app.cs"] = """
                [App("changing-app")]
                public sealed class ChangingApp : DigitalBrain.App { public int X = 2; }
                """
        };

        var r1 = _check.Compile(files1);
        var r2 = _check.Compile(files2);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.NotEqual(r1.Assemblies["app.cs"].Hash, r2.Assemblies["app.cs"].Hash);
    }
}
