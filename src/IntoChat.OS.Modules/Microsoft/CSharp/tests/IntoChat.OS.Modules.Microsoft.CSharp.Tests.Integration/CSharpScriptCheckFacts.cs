using System.Reflection;
using System.Security.Cryptography;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;
using Xunit;

using MicrosoftSdk = Microsoft;
namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class CSharpScriptCheckFacts
{
    private readonly CSharpScriptCheck _check = new();

    [Fact]
    public void ACompilingScriptWithContractsPasses()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["synapses/hello.cs"] = """
                [App("hello")]
                public sealed class HelloApp : DigitalBrain.App
                {
                    public static void Run()
                    {
                        var file = default(IntoChat.OS.Modules.Microsoft.CSharp.Files.ICSharpFile);
                        Console.WriteLine($"{file}");
                    }
                }
                """,
        });

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(error => $"{error.Id} {error.Message}")));
        Assert.Empty(result.Errors);
    }

    // The sandbox carries the runtime, the vocabulary, the client and the contracts; a behavior that
    // compiles here against anything else (the framework ring, the platform, a driver) could never load there.
    [Theory]
    [InlineData("IntoChat.OS.Framework.BrainScope.CurrentId();")]
    [InlineData("Console.WriteLine(typeof(Microsoft.CodeAnalysis.SyntaxTree));")]
    [InlineData("Console.WriteLine(typeof(MicrosoftSdk.AspNetCore.Http.HttpContext));")]
    public void ABehaviorCompilesOnlyAgainstWhatTheSandboxCarries(string line)
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["synapses/reach.cs"] = $$"""
                [App("reach")]
                public sealed class ReachApp : DigitalBrain.App
                {
                    public static void Run()
                    {
                        {{line}}
                    }
                }
                """
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Id is "CS0234" or "CS0246" or "CS0400");
    }

    [Fact]
    public void ACompilingScriptEmitsAnAssemblyNamedByItsContentHash()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["synapses/hello.cs"] = """
                [App("hello")]
                public sealed class HelloApp : DigitalBrain.App
                {
                    public static void Run() => Console.WriteLine(42);
                }
                """,
        });

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(error => $"{error.Id} {error.Message}")));
        var compiled = result.Assemblies["synapses/hello.cs"];
        Assert.NotEmpty(compiled.Assembly);
        Assert.NotEmpty(compiled.Symbols);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(compiled.Assembly)), compiled.Hash);
        var loaded = System.Reflection.Assembly.Load(compiled.Assembly);
        Assert.Null(loaded.EntryPoint);
        Assert.NotNull(AppRunner.AppTypeIn(loaded));
    }

    [Fact]
    public void TheSameSourceCompilesToTheSameHash()
    {
        var files = new Dictionary<string, string>
        {
            ["a.cs"] = """
                [App("a")]
                public sealed class AppA : DigitalBrain.App
                {
                    public static void Run() => Console.WriteLine(1);
                }
                """
        };
        Assert.Equal(_check.Compile(files).Assemblies["a.cs"].Hash, _check.Compile(files).Assemblies["a.cs"].Hash);
    }

    [Fact]
    public void AFailingScriptEmitsNothing()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["bad.cs"] = """
                [App("bad")]
                public sealed class BadApp : DigitalBrain.App
                {
                    int wrong = "text";
                }
                """
        });
        Assert.False(result.Success);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ATypeErrorNamesTheFileTheLineAndTheDiagnostic()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["tests.cs"] = """
                [App("tests")]
                public sealed class TestApp : DigitalBrain.App
                {
                    int wrong = "text";
                }
                """,
        });

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(("tests.cs", "CS0029", 4), (error.File, error.Id, error.Line));
    }

    [Fact]
    public void MultipleFilesCompileIntoSingleAssembly()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Greeting.cs"] = "public static class Greeting { public static string Of(string name) => \"hi \" + name; }\n",
            ["App.cs"] = """
                [App("main")]
                public sealed class MainApp : DigitalBrain.App
                {
                    public static void Run() => Console.WriteLine(Greeting.Of("world"));
                }
                """,
        });
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(error => error.Message)));
        var entry = Assert.Single(result.Assemblies);
        Assert.Equal("App.cs", entry.Key);
        Assert.NotEmpty(entry.Value.Assembly);
    }

    [Fact]
    public void AnErrorInASecondaryFileIsReportedAgainstThatFile()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Broken.cs"] = "public static class Broken { int x = \"no\"; }\n",
            ["App.cs"] = """
                [App("main")]
                public sealed class MainApp : DigitalBrain.App
                {
                    public static void Run() { }
                }
                """,
        });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.File == "Broken.cs" && error.Id == "CS0029");
    }

    [Fact]
    public void FilesWithoutAnAppClassAreRefusedWithDiagnostic()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Only.cs"] = "public static class Only {}\n"
        });
        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal("DB0030", error.Id);
        Assert.Equal("Only.cs", error.File);
    }

    [Fact]
    public void MultipleAppClassesAreRefusedWithDiagnostic()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Apps.cs"] = """
                [App("first")]
                public sealed class FirstApp : DigitalBrain.App { }

                [App("second")]
                public sealed class SecondApp : DigitalBrain.App { }
                """
        });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Id == "DB0030");
    }

    [Fact]
    public void CompilingNothingIsRefused()
        => Assert.Throws<ArgumentException>(() => _check.Compile(new Dictionary<string, string>()));

    // -------------------------------------------------------------------------------------------------
    // Adversarial Challenge Invariants: Single DLL & DB0030 Edge Cases
    // -------------------------------------------------------------------------------------------------

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

        var entry = Assert.Single(result.Assemblies);
        Assert.Equal("Apps/OrderApp.Part1.cs", entry.Key);

        var loaded = Assembly.Load(entry.Value.Assembly);
        Assert.Null(loaded.EntryPoint);

        Assert.NotNull(loaded.GetType("MyApp.Models.Order"));
        Assert.NotNull(loaded.GetType("MyApp.Services.IOrderService"));
        Assert.NotNull(loaded.GetType("MyApp.Services.StandardOrderService"));
        var appType = loaded.GetType("MyApp.Apps.OrderApp");
        Assert.NotNull(appType);
        Assert.Same(appType, AppRunner.AppTypeIn(loaded));
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

        var result2 = _check.Compile(files);
        Assert.True(result2.Success);
        Assert.Equal(emitsAfterFirst, _check.Emits);
        Assert.Equal(result1.Assemblies["app.cs"].Hash, result2.Assemblies["app.cs"].Hash);
        Assert.Same(result1.Assemblies["app.cs"], result2.Assemblies["app.cs"]);
    }
}
