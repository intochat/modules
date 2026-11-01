using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Testing.Signals;

[Signal("testing.suite-verified")]
public sealed record TestSuiteVerified(
    string SuiteName,
    bool Success,
    int Total,
    int Passed,
    int Failed) : Signal;
