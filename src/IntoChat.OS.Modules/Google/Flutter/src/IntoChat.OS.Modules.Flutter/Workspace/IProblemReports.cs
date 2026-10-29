using DigitalBrain.Contracts;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Flutter.Workspace;

[NeuronAlias("ui.problem-reports"), DefaultNeuronType("ui.problem-reports")]
// A brain's one problem-report list, keyed {brain}/main (R4-19 singleton key).
public interface IProblemReports
{
    Task<ProblemReport> Add(string workspaceId, string intentId, string message);
    Task<IReadOnlyList<ProblemReport>> Read();
}
