using DigitalBrain;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Programs.Signals;

// Published on every durable change of an app program: assignment, retire, watermark acknowledgement.
[Signal("microsoft.csharp.app-program-changed")]
public sealed record AppProgramChanged(string AppKey) : DigitalBrain.Signal;
