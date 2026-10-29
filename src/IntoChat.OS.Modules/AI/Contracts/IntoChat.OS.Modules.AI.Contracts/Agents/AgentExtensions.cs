using System.Runtime.CompilerServices;
using System.Text;
using IntoChat.OS.Modules.AI.Inference;

namespace IntoChat.OS.Modules.AI.Agents;

// One finished turn as a caller sees it: the reply text and the messages it produced.
public sealed record AgentReply(
    string RunId,
    string Text,
    AiMessage[] Messages,
    AgentUsage? Usage);

// The request/response shapes callers had before Run streamed; each is a view over Run.
public static class AgentExtensions
{
    public static async Task<AgentReply> Ask(this IAgent agent, AgentTurn turn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var text = new StringBuilder();
        var runId = "";
        AgentTurnEvent.Completed? completed = null;
        await foreach (var item in agent.Run(turn, ct))
        {
            switch (item)
            {
                case AgentTurnEvent.Started started: runId = started.RunId; break;
                case AgentTurnEvent.Text delta: text.Append(delta.Content); break;
                case AgentTurnEvent.Completed done: completed = done; break;
                case AgentTurnEvent.Failed failed when failed.Cancelled: throw new OperationCanceledException(failed.Message, ct);
                case AgentTurnEvent.Failed failed: throw new InvalidOperationException(failed.Message);
            }
        }
        if (completed is null) { throw new InvalidOperationException("The run ended without a completed response."); }
        return new(runId, text.ToString(), [.. completed.Messages], completed.Usage);
    }

    public static Task<AgentReply> Ask(this IAgent agent, string prompt, CancellationToken ct = default) => agent.Ask(AgentTurn.Text(prompt), ct);

    public static async Task<string> GetResponse(this IAgent agent, string prompt, CancellationToken ct = default) => (await agent.Ask(prompt, ct)).Text;

    public static Task<AgentReply> GetRichResponse(this IAgent agent, string prompt, CancellationToken ct = default) => agent.Ask(prompt, ct);

    public static Task<AgentReply> GetRichResponse(this IAgent agent, AiMessage message, CancellationToken ct = default) => agent.Ask(new AgentTurn(message), ct);

    public static IAsyncEnumerable<string> GetResponseStream(this IAgent agent, string prompt, CancellationToken ct = default)
        => agent.GetResponseStream(AgentTurn.Text(prompt).Message, ct);

    public static async IAsyncEnumerable<string> GetResponseStream(this IAgent agent, AiMessage message, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        await foreach (var item in agent.Run(new AgentTurn(message), ct))
        {
            switch (item)
            {
                case AgentTurnEvent.Text delta: yield return delta.Content; break;
                case AgentTurnEvent.Failed failed when failed.Cancelled: throw new OperationCanceledException(failed.Message, ct);
                case AgentTurnEvent.Failed failed: throw new InvalidOperationException(failed.Message);
            }
        }
    }
}
