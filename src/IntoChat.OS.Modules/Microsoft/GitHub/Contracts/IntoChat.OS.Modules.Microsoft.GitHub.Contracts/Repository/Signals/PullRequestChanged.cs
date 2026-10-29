using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.GitHub.Signals;

[DigitalBrain.Signal("github.pull-request-changed")]
public sealed record PullRequestChanged(
    int Number,
    string Version,
    string SubjectKey) : DigitalBrain.Signal;
