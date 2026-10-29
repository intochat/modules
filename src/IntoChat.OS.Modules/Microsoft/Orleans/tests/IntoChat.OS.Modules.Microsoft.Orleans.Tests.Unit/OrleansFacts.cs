namespace IntoChat.OS.Modules.Microsoft.Orleans.Tests.Unit;

public sealed class OrleansFacts
{
    [Fact]
    public void TheDefaultOptionsValidateAndKeepAzuriteOffButPersistent()
    {
        var options = new OrleansOptions();

        options.Validate();

        Assert.False(options.Azurite.Enabled);
        Assert.True(options.Azurite.PersistentStorage);
        Assert.Null(options.Azurite.DataVolume);
        Assert.False(string.IsNullOrWhiteSpace(options.GrainStateContainer));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankGrainStateContainerIsRefused(string container)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new OrleansOptions { GrainStateContainer = container }.Validate());

        Assert.Contains("GrainStateContainer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullAzuriteSectionIsRefused()
        => Assert.Throws<ArgumentNullException>(() => new OrleansOptions { Azurite = null! }.Validate());
}
