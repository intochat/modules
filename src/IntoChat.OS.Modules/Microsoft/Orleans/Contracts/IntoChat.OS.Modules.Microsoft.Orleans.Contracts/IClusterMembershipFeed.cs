namespace IntoChat.OS.Modules.Microsoft.Orleans;

// Local module boundary over Orleans' membership stream, not a grain or a polling client.
// A subscription delivers the current value first. Null denotes an unavailable feed;
// the next snapshot recovers it. Its owner must dispose it when its activation ends.
public interface IClusterMembershipFeed
{
    ClusterSnapshot Current { get; }
    IAsyncDisposable Subscribe(Func<ClusterSnapshot?, Task> changed);
}
