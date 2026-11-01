using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Tasks.Signals;

[Signal("tasks.created")]
public sealed record TaskCreated(
    string TaskId,
    string Title,
    string Owner) : Signal;

[Signal("tasks.step-advanced"), EphemeralSignal]
public sealed record TaskStepAdvanced(
    string TaskId,
    int StepIndex,
    TaskStepStatus Status) : Signal;

[Signal("tasks.completed")]
public sealed record TaskCompleted(
    string TaskId,
    TaskStatus Status) : Signal;

[Signal("tasks.rolled-back")]
public sealed record TaskRolledBack(
    string TaskId,
    int ToStepIndex,
    string Reason) : Signal;

[Signal("tasks.directory-changed")]
public sealed record TaskDirectoryChanged(
    int TaskCount,
    string? UpdatedTaskId = null) : Signal;
