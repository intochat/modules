using IntoChat.OS.Modules.AI.SystemOne;

namespace IntoChat.OS.Modules.AI.Models.TypeSafe;

// The hosted TypeSafe System One. Needs the `typesafe` integration's ApiKey.
public sealed class Jev : SystemOneModel<IJev>
{
    public override string Id => "jev-latest";

    public override AiProvider Provider => AiProvider.TypeSafe;
}

// Marker for AppHost WithSystemOne<IJev>() / WithDefaultSystemOne<IJev>().
public interface IJev : ISystemOneModel;
