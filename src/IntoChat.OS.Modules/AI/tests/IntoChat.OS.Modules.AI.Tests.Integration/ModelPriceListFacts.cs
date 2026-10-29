using IntoChat.OS.Modules.AI.Pricing;
using IntoChat.OS.Contracts.Compute;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

public sealed class ModelPriceListFacts
{
    [Fact]
    public void AKnownModelInputIsPricedPerMillionTokens()
    {
        var list = new ModelPriceList();
        Assert.Equal(1_250_000L, list.MicrosPerUnit(CostKind.Model, "gpt-5.4:input"));
    }

    [Fact]
    public void AKnownModelOutputIsPricedPerMillionTokens()
    {
        var list = new ModelPriceList();
        Assert.Equal(5_000_000L, list.MicrosPerUnit(CostKind.Model, "gpt-5.4:output"));
    }

    [Fact]
    public void AnUnknownModelIsNotPriced() => Assert.Null(new ModelPriceList().MicrosPerUnit(CostKind.Model, "nope:input"));

    [Fact]
    public void AnUnknownTokenClassIsNotPriced() => Assert.Null(new ModelPriceList().MicrosPerUnit(CostKind.Model, "gpt-5.4:nn"));

    [Fact]
    public void OnlyModelCostsArePriced() => Assert.Null(new ModelPriceList().MicrosPerUnit(CostKind.Http, "gpt-5.4:input"));
}
