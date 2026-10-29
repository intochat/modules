using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
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
                var file = default(IntoChat.OS.Modules.Microsoft.CSharp.Files.ICSharpFile);
                Console.WriteLine($"{args.Length} {file}");
                await Task.CompletedTask;
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
        var result = _check.Compile(new Dictionary<string, string> { ["synapses/reach.cs"] = line + "\nawait Task.CompletedTask;\n" });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Id is "CS0234" or "CS0246" or "CS0400");
    }

    [Fact]
    public void ACompilingScriptEmitsAnAssemblyNamedByItsContentHash()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["synapses/hello.cs"] = "Console.WriteLine(args.Length);\nawait Task.CompletedTask;\n",
        });

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(error => $"{error.Id} {error.Message}")));
        var compiled = result.Assemblies["synapses/hello.cs"];
        Assert.NotEmpty(compiled.Assembly);
        Assert.NotEmpty(compiled.Symbols);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(compiled.Assembly)), compiled.Hash);
        Assert.Equal("Program", System.Reflection.Assembly.Load(compiled.Assembly).EntryPoint!.DeclaringType!.Name);
    }

    [Fact]
    public void TheSameSourceCompilesToTheSameHash()
    {
        var files = new Dictionary<string, string> { ["a.cs"] = "Console.WriteLine(1);\n" };
        Assert.Equal(_check.Compile(files).Assemblies["a.cs"].Hash, _check.Compile(files).Assemblies["a.cs"].Hash);
    }

    [Fact]
    public void AFailingScriptEmitsNothing()
    {
        var result = _check.Compile(new Dictionary<string, string> { ["bad.cs"] = "int wrong = \"text\";\n" });
        Assert.False(result.Success);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public void ATypeErrorNamesTheFileTheLineAndTheDiagnostic()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["tests.cs"] = "var ok = 1;\nint wrong = \"text\";\n",
        });

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(("tests.cs", "CS0029", 2), (error.File, error.Id, error.Line));
    }

    [Fact]
    public void AFileOfOnlyTypesIsSharedIntoEverySynapseAndYieldsNoAssembly()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Greeting.cs"] = "public static class Greeting { public static string Of(string name) => \"hi \" + name; }\n",
            ["a.cs"] = "Console.WriteLine(Greeting.Of(\"a\"));\n",
            ["b.cs"] = "Console.WriteLine(Greeting.Of(\"b\"));\n",
        });
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(["a.cs", "b.cs"], result.Assemblies.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnErrorInASharedFileIsReportedAgainstThatFile()
    {
        var result = _check.Compile(new Dictionary<string, string>
        {
            ["Broken.cs"] = "public static class Broken { int x = \"no\"; }\n",
            ["a.cs"] = "await Task.CompletedTask;\n",
        });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.File == "Broken.cs" && error.Id == "CS0029");
    }

    [Fact]
    public void SharedFilesAloneCompileNothing()
        => Assert.Throws<ArgumentException>(() => _check.Compile(new Dictionary<string, string> { ["Only.cs"] = "public static class Only {}\n" }));

    [Fact]
    public void CompilingNothingIsRefused()
        => Assert.Throws<ArgumentException>(() => _check.Compile(new Dictionary<string, string>()));
}
