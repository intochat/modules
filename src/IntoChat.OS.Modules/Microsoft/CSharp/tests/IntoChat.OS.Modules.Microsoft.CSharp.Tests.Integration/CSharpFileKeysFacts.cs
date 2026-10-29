using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class CSharpFileKeysFacts
{
    [Fact]
    public void AKeyIsTheBrainScopeTheCSharpSegmentAndTheName()
        => Assert.Equal("alpha/csharp/report", CSharpFileKeys.For("alpha", "report"));

    [Theory]
    [InlineData("../other")]
    [InlineData("other/program")]
    [InlineData("C:\\private")]
    [InlineData("")]
    [InlineData("catalog")]
    public void ANameThatCouldReachAnotherKeyIsRefused(string name)
        => Assert.Throws<ArgumentException>(() => CSharpFileKeys.For("alpha", name));

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("a\\b")]
    public void AScopeThatIsNotABrainIdIsRefused(string scope)
        => Assert.Throws<ArgumentException>(() => CSharpFileKeys.For(scope, "report"));

    [Fact]
    public void EveryKeyBelongsToItsBrainForAccessChecks()
        => Assert.Equal("alpha", BrainKey.ScopeOf(CSharpFileKeys.For("alpha", "report")));

    // What Contracts accepts as a scope must always be a valid brain id.
    [Theory]
    [InlineData("personal")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("")]
    public void ContractsAcceptNoScopeTheFrameworkRefuses(string scope)
    {
        if (Record.Exception(() => CSharpFileKeys.For(scope, "x")) is null) { Assert.True(BrainScope.IsValidId(scope)); }
    }
}
