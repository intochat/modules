using System.Text.Json.Serialization;

namespace IntoChat.OS.Modules.AI.SystemOne;

// The typed answer to a System One question. Choice and Score carry a full probability
// distribution and a confidence (how peaked that distribution is); Noul carries a truth value.
// The JSON form (plain neuron calls and signals, 06-05b) names the case, as the Orleans form names its alias.
[JsonPolymorphic, JsonDerivedType(typeof(Choice), "choice"), JsonDerivedType(typeof(Score), "score"), JsonDerivedType(typeof(Noul), "noul")]
public abstract record SystemOneAnswer
{
    public sealed record Choice(string Value, Dictionary<string, double> Probabilities, double Confidence) : SystemOneAnswer;

    public sealed record Score(double Value, Dictionary<string, double> Probabilities, double Confidence) : SystemOneAnswer;

    public sealed record Noul(double Value) : SystemOneAnswer;
}
