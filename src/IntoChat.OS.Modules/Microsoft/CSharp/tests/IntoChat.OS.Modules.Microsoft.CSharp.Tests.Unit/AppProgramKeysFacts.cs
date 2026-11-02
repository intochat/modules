using IntoChat.OS.Modules.Microsoft.CSharp.Programs;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class AppProgramKeysFacts
{
    [Fact]
    public void ForBrainAndAppReturnsCanonicalKey()
    {
        Assert.Equal("alpha/app/todo", AppProgramKeys.For("alpha", "todo"));
        Assert.Equal("alpha/app/my-app", AppProgramKeys.For("alpha", "my-app"));
        Assert.Equal("workspace_1/app/app_2", AppProgramKeys.For("workspace_1", "app_2"));
    }

    [Fact]
    public void ForBrainAndPrefixedAppStripsPrefix()
    {
        Assert.Equal("alpha/app/todo", AppProgramKeys.For("alpha", "apps/todo"));
        Assert.Equal("alpha/app/todo", AppProgramKeys.For("alpha", "app/todo"));
    }

    [Theory]
    [InlineData("alpha", "alpha/apps/todo", "alpha/app/todo")]
    [InlineData("alpha", "alpha/app/todo", "alpha/app/todo")]
    [InlineData("alpha", "alpha/todo", "alpha/app/todo")]
    [InlineData("alpha", "apps/todo", "alpha/app/todo")]
    [InlineData("alpha", "app/todo", "alpha/app/todo")]
    [InlineData("alpha", "todo", "alpha/app/todo")]
    [InlineData("workspace_1", "workspace_1/apps/todo", "workspace_1/app/todo")]
    [InlineData("workspace_1", "workspace_1/app/todo", "workspace_1/app/todo")]
    public void ForBrainAndAppNormalizesFullAndPrefixedKeys(string brainId, string appName, string expected)
    {
        Assert.Equal(expected, AppProgramKeys.For(brainId, appName));
    }

    [Fact]
    public void ForBrainAndMismatchedBrainPrefixThrows()
    {
        Assert.Throws<ArgumentException>(() => AppProgramKeys.For("alpha", "beta/apps/todo"));
        Assert.Throws<ArgumentException>(() => AppProgramKeys.For("alpha", "beta/app/todo"));
    }

    [Theory]
    [InlineData("alpha/app/todo", "alpha/app/todo")]
    [InlineData("alpha/apps/todo", "alpha/app/todo")]
    [InlineData("alpha/todo", "alpha/app/todo")]
    public void ForAppKeyNormalizesToCanonicalFormat(string input, string expected)
    {
        Assert.Equal(expected, AppProgramKeys.For(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("alpha/")]
    [InlineData("alpha/app/")]
    [InlineData("alpha/apps/")]
    [InlineData("alpha/app/bad/name")]
    [InlineData("alpha/app/bad:name")]
    [InlineData("alpha/app/bad\\name")]
    public void InvalidAppKeysAreRefusedByFor(string invalidKey)
    {
        Assert.Throws<ArgumentException>(() => AppProgramKeys.For(invalidKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("a\\b")]
    public void InvalidBrainIdIsRefusedByFor(string invalidBrain)
    {
        Assert.Throws<ArgumentException>(() => AppProgramKeys.For(invalidBrain, "todo"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("a\\b")]
    public void InvalidAppNameIsRefusedByFor(string invalidApp)
    {
        Assert.Throws<ArgumentException>(() => AppProgramKeys.For("alpha", invalidApp));
    }

    [Fact]
    public void TryParseExtractsBrainAndAppName()
    {
        Assert.True(AppProgramKeys.TryParse("alpha/app/todo", out var brain, out var app));
        Assert.Equal("alpha", brain);
        Assert.Equal("todo", app);

        Assert.True(AppProgramKeys.TryParse("gamma/apps/gallery", out brain, out app));
        Assert.Equal("gamma", brain);
        Assert.Equal("gallery", app);

        Assert.True(AppProgramKeys.TryParse("delta/creator", out brain, out app));
        Assert.Equal("delta", brain);
        Assert.Equal("creator", app);

        Assert.False(AppProgramKeys.TryParse("invalid-no-slash", out _, out _));
        Assert.False(AppProgramKeys.TryParse("/starts-with-slash", out _, out _));
        Assert.False(AppProgramKeys.TryParse("alpha/app/nested/path", out _, out _));
        Assert.False(AppProgramKeys.TryParse("csharp/script.cs", out _, out _));
        Assert.False(AppProgramKeys.TryParse("alpha/csharp/script.cs", out _, out _));
        Assert.False(AppProgramKeys.TryParse("apps/todo", out _, out _));
        Assert.False(AppProgramKeys.TryParse("app/todo", out _, out _));
    }

    [Fact]
    public void ParseExtractsTupleOrThrows()
    {
        var (brain, app) = AppProgramKeys.Parse("alpha/app/todo");
        Assert.Equal("alpha", brain);
        Assert.Equal("todo", app);

        Assert.Throws<ArgumentException>(() => AppProgramKeys.Parse("invalid"));
    }

    [Fact]
    public void AppNameOfAndBrainOfHelpersWork()
    {
        Assert.Equal("todo", AppProgramKeys.AppNameOf("alpha/app/todo"));
        Assert.Equal("alpha", AppProgramKeys.BrainOf("alpha/app/todo"));
    }
}
