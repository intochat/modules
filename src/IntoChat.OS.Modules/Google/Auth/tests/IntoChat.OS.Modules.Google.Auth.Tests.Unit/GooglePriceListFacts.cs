using System.Reflection;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Modules.Metering;
using IntoChat.OS.Modules.Google.Drive;
using IntoChat.OS.Modules.Google.Gmail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Google.Auth.Tests.Unit;

public sealed class GooglePriceListFacts
{
    [Theory]
    [InlineData(typeof(GoogleAuthModule))]
    [InlineData(typeof(GmailModule))]
    [InlineData(typeof(DriveModule))]
    public void TheDeclaredPriceListKeepsUnknownProviderPricesDistinctFromZero(Type module)
    {
        var services = new ServiceCollection();
        module.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Static, [typeof(IServiceCollection), typeof(IConfiguration)])!
            .Invoke(null, [services, new ConfigurationBuilder().Build()]);
        var registration = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ModulePriceListRegistration>());
        Assert.Equal(typeof(GooglePriceList), registration.PriceListType);
        var prices = Assert.IsAssignableFrom<IPriceList>(Activator.CreateInstance(registration.PriceListType));
        Assert.NotEmpty(registration.Operations);
        foreach (var operation in registration.Operations)
        {
            var quote = prices.Quote(CostKind.ExternalProvider, operation.Name, operation.Unit);
            Assert.Null(quote.MicrosPerUnit);
            Assert.Equal(PricingStatus.Unpriced, quote.Status);
            Assert.Equal(ExternalChargePolicy.Informational, quote.ChargePolicy);
            Assert.Null(quote.EffectiveFrom);
            Assert.NotNull(quote.Source);
        }
    }
}
