using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.ImageCanvas;

[Neuron(UIVocabulary.ImageCanvasType)]
internal sealed class ImageCanvasNeuron
    : UiNeuron<ImageCanvasState>, IImageCanvas
{
    public async Task Set(ImageCanvasDefinition definition, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.AssetId);
        definition.Recipe.Validate(definition.Width, definition.Height);
        if (State.Revision != expectedRevision) { throw new RevisionConflictException("The UI changed. Read its latest revision before retrying.", State.Revision); }
        // The state and its signal are one journal entry (D-02).
        var next = new ImageCanvasState { Name = FullKey, Revision = checked(State.Revision + 1), Definition = definition };
        await Save(next, new ImageCanvasChanged(FullKey, next.Revision));
    }

    public async Task RequestEdit(ImageRecipe recipe, long expectedDocumentRevision, string operationId)
    {
        if (State.Definition.DocumentRevision != expectedDocumentRevision) { throw new InvalidOperationException("The image changed. Reload before editing."); }
        if (!Guid.TryParse(operationId, out _)) { throw new ArgumentException("An edit requires a unique operation ID."); }
        recipe.Validate(State.Definition.Width, State.Definition.Height);
        await Publish(new ImageCanvasEditRequested(FullKey, recipe, expectedDocumentRevision, operationId));
    }
    public Task<ImageCanvasState> Read() => Task.FromResult(State);
}
