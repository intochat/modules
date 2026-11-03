using System;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Access;
using DigitalBrain.Contracts;
using Orleans;

namespace IntoChat.OS.Modules.AI.Agents.Runs;

[GenerateSerializer, Alias("agent.run.state")]
public sealed record RunState(
    [property: Id(0)] string Target,
    [property: Id(1)] string? TaskId,
    [property: Id(2)] string Status,
    [property: Id(3)] DateTimeOffset Started,
    [property: Id(4)] DateTimeOffset LastTurn)
{
    public RunState() : this("", null, "Idle", default, default) { }
}

[Signal("agent.run.changed")]
public sealed record RunChanged(
    string RunKey,
    string Target,
    string? TaskId,
    string Status,
    DateTimeOffset LastTurn) : Signal;

[NeuronAlias("agent.run"), DefaultNeuronType("agent.run")]
[Shareable]
public interface IRunNeuron
{
    Task<RunState> StartAsync(string target, string? taskId = null);

    Task<RunState> TouchAsync(string? status = null);

    Task<RunState> CompleteAsync(string status = "Completed");

    [ReadOnlyCall]
    Task<RunState> ReadAsync();
}
