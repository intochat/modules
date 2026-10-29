using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Routing;

// Ask's answer runs as a one-way call to the router itself, so the poster returns at once (a deferred turn would hold
// the poster: the host drains deferred work before the call returns).
[NeuronAlias("ai.router-work")]
internal interface IRouterWork
{
    [DigitalBrain.OneWayCall] Task Decide(Guid id);
}
