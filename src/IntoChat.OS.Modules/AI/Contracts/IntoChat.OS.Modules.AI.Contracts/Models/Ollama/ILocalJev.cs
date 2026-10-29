using IntoChat.OS.Modules.AI.SystemOne;

namespace IntoChat.OS.Modules.AI.Models.Ollama;

// The systemone-adapter container speaking Jev's wire shape, answered by this Ollama model.
// Id is the Ollama model the adapter is told to use (SYSTEMONE_MODEL), the compose default.
public sealed class LocalJev : SystemOneModel<ILocalJev>
{
    public override string Id => "llama3.1";

    public override AiProvider Provider => AiProvider.Ollama;
}

// Marker for AppHost WithDefaultSystemOne<ILocalJev>(): hosts the adapter as the `systemone` resource.
public interface ILocalJev : ISystemOneModel;
