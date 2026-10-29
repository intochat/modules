namespace IntoChat.OS.Modules.Microsoft.GitHub.Tests.Unit;

public sealed class GitHubFacts
{
    [Fact]
    public void ThePullRequestChangesAreDistinctBits()
    {
        var all = Enum.GetValues<PullRequestChange>();

        Assert.Equal(all.Length, all.Select(change => (int)change).Distinct().Count());
        Assert.All(all, change => Assert.Equal(1, int.PopCount((int)change)));
    }

    [Fact]
    public void ChangesCombineAndTestAsFlags()
    {
        var watched = PullRequestChange.Opened | PullRequestChange.Checks;

        Assert.True(watched.HasFlag(PullRequestChange.Checks));
        Assert.False(watched.HasFlag(PullRequestChange.Closed));
        Assert.Equal(9, (int)watched);
    }

    [Fact]
    public void ANewConfigurationIsNotConfiguredAndValidates()
    {
        var options = new GitHubModuleOptions();

        options.Validate();

        Assert.Equal(0, options.AppId);
        Assert.Equal("", options.PublicWebhookUrl);
        Assert.Null(options.PublicOrigin);
    }

    [Fact]
    public void AnHttpsWebhookUrlIsAccepted()
        => new GitHubModuleOptions { PublicWebhookUrl = "https://hooks.example.test/github" }.Validate();

    [Theory]
    [InlineData("http://hooks.example.test/github")]
    [InlineData("hooks.example.test/github")]
    [InlineData("ftp://hooks.example.test")]
    public void AWebhookUrlThatIsNotAbsoluteHttpsIsRefused(string url)
        => Assert.Throws<ArgumentException>(() => new GitHubModuleOptions { PublicWebhookUrl = url }.Validate());

    [Fact]
    public void TheSectionNameIsTheGithubModulesOptionsSection()
    {
        Assert.Contains("github", GitHubModuleOptions.SectionName, StringComparison.OrdinalIgnoreCase);
    }
}
