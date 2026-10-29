using DigitalBrain;
using IntoChat.OS.Modules.Flutter.Workspace;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Viewport;

[Neuron("viewport")]
internal sealed class ViewportNeuron
    : UiNeuron<ViewportStorage>, IViewport
{
    public Task<ViewportState> Read() => Task.FromResult(new ViewportState(FullKey, Snapshot.Revision, Snapshot.Target));

    public async Task Set(WorkspaceViewRef? target, long expectedRevision)
    {
        var name = FullKey;
        var marker = name.IndexOf("/apps/", StringComparison.Ordinal);
        if (marker <= 0) { throw new ArgumentException("A viewport belongs to an app."); }
        var prefix = name[..(marker + 6)];
        var slash = name.IndexOf('/', prefix.Length);
        var owner = slash < 0 ? name : name[..slash];
        if (target is not null && (string.IsNullOrWhiteSpace(target.Id) || target.Id.Length > 128
            || string.IsNullOrWhiteSpace(target.Variant) || target.Variant.Length > 64 || target.Context != ""
            || string.IsNullOrWhiteSpace(target.App) || !target.App.StartsWith(prefix, StringComparison.Ordinal)
            || target.App.Length > 512 || target.App[prefix.Length..].Contains('/')
            || target.App[prefix.Length..] is "" or "." or ".."
            // Never its own app: that app's view may contain this viewport and recurse.
            || target.App == owner))
        { throw new ArgumentException("This viewport supports local app views without an external context."); }
        if (Snapshot.Revision != expectedRevision)
        { throw new RevisionConflictException("Read the current viewport before changing its target.", Snapshot.Revision); }
        if (Snapshot.Target == target) { return; }
        var next = new ViewportStorage { Revision = checked(Snapshot.Revision + 1), Target = target };
        await Save(next, new ViewportChanged(name, next.Revision));
    }
}

[GenerateSerializer, Alias("ui.viewport-storage")]
[ExportableState]
internal sealed class ViewportStorage
{
    [Id(0)] public long Revision { get; set; }
    [Id(1)] public WorkspaceViewRef? Target { get; set; }
}
