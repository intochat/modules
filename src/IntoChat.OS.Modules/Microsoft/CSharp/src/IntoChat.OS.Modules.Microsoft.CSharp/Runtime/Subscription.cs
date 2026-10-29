namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// One durable subscription a behavior declared through its session. Pending[i] is signal number
// Delivered + 1 + i: delivery is at-least-once, so a batch stays here until it is acknowledged and an
// unacknowledged batch is resent on the next stream. Past the cap the oldest signal is dropped and
// counted, never silently. Arrays, not interface lists: grain storage round-trips this state through JSON.
// On a machine SourceSeq is the watermark against the source: the part-log seq of the last source entry buffered.
[GenerateSerializer, Alias("microsoft.csharp.runtime-subscription")]
internal sealed record Subscription(
    [property: Id(0)] string Id,
    [property: Id(1)] string Behavior,
    [property: Id(2)] string Neuron,
    [property: Id(3)] string Signal,
    [property: Id(4)] string[] Pending,
    [property: Id(5)] long Delivered,
    [property: Id(6)] long Dropped,
    [property: Id(7)] long SourceSeq = 0,
    [property: Id(8)] string? Selector = null)
{
    public long Watermark => Delivered + Pending.Length;

    public Subscription Arrive(string json, int cap)
        => Pending.Length < cap
            ? this with { Pending = [.. Pending, json] }
            : this with { Pending = [.. Pending.Skip(1), json], Delivered = Delivered + 1, Dropped = Dropped + 1 };

    public Subscription Acknowledge(long watermark)
    {
        var trim = (int)Math.Clamp(watermark - Delivered, 0, Pending.Length);
        return trim == 0 ? this : this with { Pending = Pending[trim..], Delivered = Delivered + trim };
    }
}
