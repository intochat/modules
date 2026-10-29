
namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// A whole program of one owner: every synapse of an app (or a standalone file's one program). The brain
// runtime compiles, stores and assigns it in one call, so a write that does not compile never reaches a
// running behavior.
// A plain contract of the brain runtime (06-08); [Internal] as a whole: module code (the Apps runtime, the C# files) assigns
// programs, no edge caller does.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.programs"), DigitalBrain.Contracts.DefaultNeuronType("microsoft.csharp.runtime")]
[DigitalBrain.Access.Internal]
public interface IBrainPrograms
{
    // Interleaved: compiling and storing touch none of the runtime's state, so its other calls go on meanwhile;
    // the compiled program is applied in a turn of its own.
    [DigitalBrain.Interleaves] Task<ProgramView> Assign(string owner, Dictionary<string, string> sources, Dictionary<string, string> settings);
    // A program built ahead of time (MSBuild). One verified assembly per path; the hash must be the SHA-256 of the
    // assembly bytes, the same name the sandbox verifies on download. Nothing is compiled; storing and assigning are as Assign.
    [DigitalBrain.Interleaves] Task<ProgramView> AssignPrebuilt(string owner, Dictionary<string, PrebuiltBehavior> behaviors, Dictionary<string, string> settings);
    // A call carries at most a megabyte, so a large assembly is staged in chunks under its hash first: AssignPrebuilt then names it with
    // an empty Assembly. The last chunk checks the whole against the hash and stores it; a mismatch discards it.
    [DigitalBrain.Interleaves] Task Stage(string hash, int offset, byte[] chunk, bool last);
    Task Retire(string owner);
    // Waits until each synapse of the owner's program has reported, running, failed to update (the sandbox could not stop the
    // previous build and kept it: the previous program is assigned again) or exited, or the timeout; answers the program as it
    // then stands, null when none is assigned. A synapse still Starting at the timeout did not come up.
    [DigitalBrain.Interleaves] Task<ProgramView?> Settled(string owner, TimeSpan timeout);
    // A pure read: it never waits for a turn in progress.
    [DigitalBrain.Interleaves] Task<ProgramView?> Describe(string owner);
}

public sealed record PrebuiltBehavior(byte[] Assembly, byte[] Symbols, string Hash);

public sealed record ProgramView(
    string Owner,
    string Hash,
    BehaviorView[] Behaviors);

public static class BehaviorIds
{
    // A behavior is one synapse of one owner's program: "{owner}/{path}". A standalone file's one
    // program has the empty path, so its behavior id is its own key.
    public static string For(string owner, string path) => path.Length == 0 ? owner : owner + "/" + path;
}
