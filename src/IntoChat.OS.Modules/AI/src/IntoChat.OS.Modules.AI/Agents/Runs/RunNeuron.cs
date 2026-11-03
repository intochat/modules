using System;
using System.Threading.Tasks;
using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Agents.Runs;

[Neuron("agent.run", State = "agent-run")]
public sealed class RunNeuron : Neuron<RunState>, IRunNeuron
{
    public async Task<RunState> StartAsync(string target, string? taskId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var now = Context.Time.GetUtcNow();
        var next = new RunState(
            Target: target,
            TaskId: taskId,
            Status: "Running",
            Started: State.Started == default ? now : State.Started,
            LastTurn: now);
        await Save(next, new RunChanged(Id.ToString(), next.Target, next.TaskId, next.Status, next.LastTurn)).ConfigureAwait(false);
        return next;
    }

    public async Task<RunState> TouchAsync(string? status = null)
    {
        var now = Context.Time.GetUtcNow();
        var next = State with
        {
            Status = string.IsNullOrWhiteSpace(status) ? (string.IsNullOrWhiteSpace(State.Status) || State.Status == "Idle" ? "Running" : State.Status) : status,
            Started = State.Started == default ? now : State.Started,
            LastTurn = now,
        };
        await Save(next, new RunChanged(Id.ToString(), next.Target, next.TaskId, next.Status, next.LastTurn)).ConfigureAwait(false);
        return next;
    }

    public async Task<RunState> CompleteAsync(string status = "Completed")
    {
        var now = Context.Time.GetUtcNow();
        var next = State with
        {
            Status = string.IsNullOrWhiteSpace(status) ? "Completed" : status,
            Started = State.Started == default ? now : State.Started,
            LastTurn = now,
        };
        await Save(next, new RunChanged(Id.ToString(), next.Target, next.TaskId, next.Status, next.LastTurn)).ConfigureAwait(false);
        return next;
    }

    public Task<RunState> ReadAsync() => Task.FromResult(State);
}
