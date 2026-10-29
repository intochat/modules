using System.Reflection;
using DigitalBrain.Spi;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Modules.AI.Models.OpenAI;
using IntoChat.OS.Modules.AI.Models.Signals;

namespace IntoChat.OS.Modules.AI.Tests.Integration.Models;

// R4-03: the model neurons' former Watch/Unwatch overrides took the publish gate only; the subscriber rule now lives in
// AdmitSynapse, which the runtime asks before it creates a synapse (NeuronAttachment.Admit). A refused subscriber gets none.
public sealed class LlmAdmissionFacts
{
    private static readonly DigitalBrain.NeuronId Model = new(new DigitalBrain.BrainId("owner"), "ai.llm", "models");

    [Fact]
    public async Task ASubscriberOfTheModelsBrainIsAdmitted()
    {
        var neuron = Attached(new LlmNeuron(null!), Model);
        await NeuronAttachment.Admit(neuron, new(Model.Brain, "agent", "agents/main"), [typeof(InferenceStarted), typeof(InferenceCompleted)]);
    }

    [Fact]
    public async Task ASubscriberOfAnotherBrainIsRefused()
    {
        var neuron = Attached(new LlmNeuron(null!), Model);
        var refused = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            NeuronAttachment.Admit(neuron, new(new DigitalBrain.BrainId("other"), "agent", "agents/main"), [typeof(InferenceStarted)]));
        Assert.Contains("another brain", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATypedModelKeepsTheSameRule()
    {
        var id = new DigitalBrain.NeuronId(new DigitalBrain.BrainId("owner"), "ai.llm.gpt56luna", "models");
        var neuron = Attached(new Gpt56LunaNeuron(null!), id);
        await NeuronAttachment.Admit(neuron, new(id.Brain, "agent", "agents/main"), [typeof(InferenceFailed)]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            NeuronAttachment.Admit(neuron, new(new DigitalBrain.BrainId("other"), "agent", "agents/main"), [typeof(InferenceFailed)]));
    }

    private static DigitalBrain.Neuron Attached(DigitalBrain.Neuron neuron, DigitalBrain.NeuronId id)
    {
        var context = DispatchProxy.Create<INeuronContext, IdOnlyContext>();
        ((IdOnlyContext)(object)context).Id = id;
        NeuronAttachment.Attach(neuron, context);
        return neuron;
    }

    // Admission reads only the neuron's own id; anything else the context is asked for is a test failure.
    public class IdOnlyContext : DispatchProxy
    {
        public DigitalBrain.NeuronId Id { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == "get_Id" ? Id : throw new NotSupportedException($"Admission asked the context for {targetMethod?.Name}.");
    }
}

// T-06-14: the router never offers a plain neuron's admission hook (or the legacy observer methods) as a routable method.
public sealed class RouterNotCallableFacts
{
    [Fact]
    public void TheRouterNeverRoutesAdmissionOrTheObserverPath()
        => Assert.Superset(new HashSet<string>(["AdmitSynapse", "Watch", "Unwatch"], StringComparer.Ordinal), IntoChat.OS.Modules.AI.Routing.RouterNeuron.NotCallable);
}
