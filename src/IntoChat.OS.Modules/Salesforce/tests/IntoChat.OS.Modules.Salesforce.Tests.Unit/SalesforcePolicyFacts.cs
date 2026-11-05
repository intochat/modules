using System.Text.Json;
using DigitalBrain;
using DigitalBrain.Capabilities.Connections;
using DigitalBrain.Capabilities.Connections.Sealing;
using DigitalBrain.Capabilities.Secrets;
using DigitalBrain.Rules;
using DigitalBrain.Spi;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Salesforce.Tests.Unit;

public sealed class SalesforcePolicyFacts
{
    private static readonly NeuronId PoliciesId = NeuronId.Parse(IPolicies.Key("b"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task Adopt(TestOS os, PolicyRule rule) => os.AdoptPolicy("b", rule, Ct);

    private static async Task<(TestOS Os, SalesforceNeuron Neuron, FakeSalesforceProvider Provider)> CreateHarnessAsync()
    {
        var os = TestOS.Local().Register(typeof(SalesforceModule).Assembly);
        var provider = new FakeSalesforceProvider();
        var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<ISecretSealer>(new PassthroughSealer())
            .BuildServiceProvider();

        var id = new NeuronId(new BrainId("b"), "salesforce", "connection/salesforce/c1");
        var context = new FakeNeuronContext(id, services);
        context.Proxies.Register(PoliciesId, os.Get<IPolicies>(PoliciesId));
        await context.MemoryState.Save(
            new SalesforceState
            {
                Status = ConnectionStatus.Connected,
                Account = "https://acme.my.salesforce.com/",
                Secrets = new(StringComparer.Ordinal)
                {
                    ["access"] = new SealedSecret { KeyId = "mk", Cipher = "sf-token" },
                },
            },
            []);

        var neuron = new SalesforceNeuron(provider, TimeProvider.System);
        NeuronAttachment.Attach(neuron, context);
        await NeuronAttachment.Activate(neuron, CancellationToken.None);
        return (os, neuron, provider);
    }

    [Fact]
    public async Task ARefusingPolicyStopsTheWriteBeforeTheApiCall()
    {
        var (os, neuron, provider) = await CreateHarnessAsync();
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "salesforce.record.write",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.None, null),
            0m,
            null,
            "Never write a Salesforce record",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        var preview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Acme"}"""));
        await Assert.ThrowsAsync<RuleViolationException>(
            () => neuron.ConfirmWrite(new ConfirmSalesforceWrite(preview.PreviewId, preview.ToolSchemaHash)));

        Assert.Equal(0, provider.WriteCalls);
    }

    [Fact]
    public async Task AnAllowedWriteIsCountedOnce()
    {
        var (os, neuron, provider) = await CreateHarnessAsync();
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "salesforce.record.write",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.Day, null),
            1m,
            null,
            "Allow at most 1 count of write a Salesforce record per day",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        var firstPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Acme"}"""));
        var first = await neuron.ConfirmWrite(new ConfirmSalesforceWrite(firstPreview.PreviewId, firstPreview.ToolSchemaHash));
        Assert.Equal("001xx000003DGbYAAW", first.RecordId);
        Assert.Equal(1, provider.WriteCalls);

        var secondPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Beta"}"""));
        await Assert.ThrowsAsync<RuleViolationException>(
            () => neuron.ConfirmWrite(new ConfirmSalesforceWrite(secondPreview.PreviewId, secondPreview.ToolSchemaHash)));
        Assert.Equal(1, provider.WriteCalls);
    }

    [Fact]
    public async Task AFailedApiCallReleasesTheReservation()
    {
        var (os, neuron, provider) = await CreateHarnessAsync();
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "salesforce.record.write",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.Day, null),
            1m,
            null,
            "Allow at most 1 count of write a Salesforce record per day",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        provider.ThrowOnNextWrite = true;
        var firstPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Acme"}"""));
        await Assert.ThrowsAsync<SalesforceUnreachableException>(
            () => neuron.ConfirmWrite(new ConfirmSalesforceWrite(firstPreview.PreviewId, firstPreview.ToolSchemaHash)));

        var secondPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Beta"}"""));
        var second = await neuron.ConfirmWrite(new ConfirmSalesforceWrite(secondPreview.PreviewId, secondPreview.ToolSchemaHash));
        Assert.Equal("001xx000003DGbYAAW", second.RecordId);

        var thirdPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Gamma"}"""));
        await Assert.ThrowsAsync<RuleViolationException>(
            () => neuron.ConfirmWrite(new ConfirmSalesforceWrite(thirdPreview.PreviewId, thirdPreview.ToolSchemaHash)));
    }

    [Fact]
    public async Task AnUncertainWriteKeepsTheReservation()
    {
        var (os, neuron, provider) = await CreateHarnessAsync();
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "salesforce.record.write",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.Day, null),
            1m,
            null,
            "Allow at most 1 count of write a Salesforce record per day",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        provider.InvalidShapeOnNextWrite = true;
        var firstPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Acme"}"""));
        var first = await neuron.ConfirmWrite(new ConfirmSalesforceWrite(firstPreview.PreviewId, firstPreview.ToolSchemaHash));
        Assert.Null(first.RecordId);

        // Salesforce answered, so the record may exist: the day's one write stays spent.
        var secondPreview = await neuron.PrepareWrite(new PrepareSalesforceWrite("createRecord", """{"objectName":"Account","Name":"Beta"}"""));
        await Assert.ThrowsAsync<RuleViolationException>(
            () => neuron.ConfirmWrite(new ConfirmSalesforceWrite(secondPreview.PreviewId, secondPreview.ToolSchemaHash)));
        Assert.Equal(1, provider.WriteCalls);
    }

    [Fact]
    public void TheManifestDeclaresTheAction()
    {
        var action = Assert.Single(ModuleManifest.Of(typeof(SalesforceModule)).Actions, a => a.Name == "salesforce.record.write");
        Assert.Equal("write a Salesforce record", action.Description);
        Assert.Null(action.AmountUnit);
    }

    private sealed class FakeSalesforceProvider : ISalesforceProvider
    {
        public int WriteCalls { get; private set; }
        public bool ThrowOnNextWrite { get; set; }
        public bool InvalidShapeOnNextWrite { get; set; }

        public Task<string> ReadToolSchemaHashAsync(string tool, string accessToken, CancellationToken cancellationToken)
            => Task.FromResult("schema-hash-v1");

        public Task<JsonElement> InvokeAsync(string tool, JsonElement arguments, string accessToken, CancellationToken cancellationToken)
        {
            WriteCalls++;
            if (ThrowOnNextWrite)
            {
                ThrowOnNextWrite = false;
                throw new SalesforceUnreachableException("Simulated Salesforce outage.");
            }
            if (InvalidShapeOnNextWrite)
            {
                InvalidShapeOnNextWrite = false;
                return Task.FromResult(JsonSerializer.SerializeToElement("not an object"));
            }
            return Task.FromResult(JsonSerializer.SerializeToElement(new { id = "001xx000003DGbYAAW" }));
        }
    }

    private sealed class PassthroughSealer : ISecretSealer
    {
        public SealedSecret Seal(string name, string plaintext) => new() { KeyId = "mk", Cipher = plaintext };
        public string Open(SealedSecret secret) => secret.Cipher;
    }
}
