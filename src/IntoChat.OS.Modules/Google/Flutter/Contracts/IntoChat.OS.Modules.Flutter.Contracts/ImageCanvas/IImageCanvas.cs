using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.ImageCanvas;

[NeuronAlias("imagecanvas"), DefaultNeuronType(UIVocabulary.ImageCanvasType)]
public interface IImageCanvas
{
    Task Set(ImageCanvasDefinition definition, long expectedRevision);
    Task RequestEdit(ImageRecipe recipe, long expectedDocumentRevision, string operationId);
    [DigitalBrain.ReadOnlyCall] Task<ImageCanvasState> Read();
}

public sealed record ImageCanvasDefinition(string AssetId, int Width, int Height, ImageRecipe Recipe, long DocumentRevision);

[ExportableState]
public sealed class ImageCanvasState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public ImageCanvasDefinition Definition { get; set; } = new("", 0, 0, new(), 0);
}

[Signal("ui.imagecanvas-changed")]
public sealed record ImageCanvasChanged(string Name, long Revision) : DigitalBrain.Signal;

[Signal("ui.imagecanvas-edit-requested")]
public sealed record ImageCanvasEditRequested(string Name, ImageRecipe Recipe, long DocumentRevision, string OperationId) : DigitalBrain.Signal;
