using DigitalBrain;
using DigitalBrain.Confirmation;
using DigitalBrain.Spi;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

// The Gmail neuron is plain (06-08): a save that fails leaves its state as it was, and the next save starts from that state.
public sealed class GmailStateFacts
{
    [Fact]
    public async Task FailedAggregateWritePreservesThePreviousSnapshotOnTheSameActivation()
    {
        var context = new FaultingContext(new DigitalBrain.NeuronId(new DigitalBrain.BrainId("facts"), "gmail", "connection/gmail/c1"),
            new GmailState { GrantGeneration = 7, BoundMailbox = "fixture@example.test" });
        var neuron = new GmailNeuron();
        NeuronAttachment.Attach(neuron, context);
        await NeuronAttachment.Activate(neuron, CancellationToken.None);
        var initial = Assert.IsType<GmailState>(context.Stored);
        context.Fail = true;

        await Assert.ThrowsAsync<IOException>(() => neuron.SaveGmailStateAsync(next => next.GrantGeneration = 8));
        Assert.Same(initial, context.Stored);
        Assert.Equal(7, initial.GrantGeneration);

        context.Fail = false;
        await neuron.SaveGmailStateAsync(next => next.GrantGeneration = 9);
        var stored = Assert.IsType<GmailState>(context.Stored);
        Assert.NotSame(initial, stored);
        Assert.Equal(7, initial.GrantGeneration);
        Assert.Equal(9, stored.GrantGeneration);
        Assert.Equal("fixture@example.test", stored.BoundMailbox);
    }

    // A fake context whose state store can be made to fail; everything else is the testing kit's fake.
    private sealed class FaultingContext(DigitalBrain.NeuronId id, GmailState initial) : INeuronContext, INeuronState
    {
        private readonly FakeNeuronContext _inner = new(id, new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(TimeProvider.System).BuildServiceProvider());

        public bool Fail { get; set; }
        public object? Stored { get; private set; } = initial;

        public DigitalBrain.NeuronId Id => _inner.Id;
        public IServiceProvider Services => _inner.Services;
        public TimeProvider Time => _inner.Time;
        public INeuronState State => this;
        public ITimers Timers => ((INeuronContext)_inner).Timers;
        public IReminders Reminders => ((INeuronContext)_inner).Reminders;
        public IOperations Operations => ((INeuronContext)_inner).Operations;
        public ISynapses Synapses => ((INeuronContext)_inner).Synapses;
        public ISignalHistory History => ((INeuronContext)_inner).History;
        public IConfirmations Confirmations => ((INeuronContext)_inner).Confirmations;
        public Task Publish(DigitalBrain.Signal signal) => _inner.Publish(signal);
        public T Get<T>(DigitalBrain.NeuronId target) where T : class => _inner.Get<T>(target);
        public void Defer(Func<Task> work) => _inner.Defer(work);

        public Task<T?> Load<T>() => Task.FromResult(Stored is T value ? value : default);

        public Task Save<T>(T next, IReadOnlyList<DigitalBrain.Signal> signals)
        {
            if (Fail) { return Task.FromException(new IOException("Fixture storage unavailable.")); }
            Stored = next;
            return Task.CompletedTask;
        }

        public Task Clear() { Stored = null; return Task.CompletedTask; }
    }
}
