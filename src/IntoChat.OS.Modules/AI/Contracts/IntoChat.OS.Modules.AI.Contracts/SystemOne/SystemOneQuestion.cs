using System.Text.Json.Serialization;

namespace IntoChat.OS.Modules.AI.SystemOne;

// A TypeSafe System One question. Choice picks one option, Score rates on a rubric, Noul asks
// whether a statement is true. A request mixes them; they are answered in parallel and in isolation.
// The JSON form (plain neuron calls and signals, 06-05b) names the case, as the Orleans form names its alias.
[JsonPolymorphic, JsonDerivedType(typeof(Choice), "choice"), JsonDerivedType(typeof(Score), "score"), JsonDerivedType(typeof(Noul), "noul")]
public abstract record SystemOneQuestion
{
    public sealed record Choice(string Instructions, Dictionary<string, string?> Criteria) : SystemOneQuestion;

    public sealed record Score(string Instructions, string[] Criteria) : SystemOneQuestion;

    public sealed record Noul(string Instructions) : SystemOneQuestion;
}
