using DigitalBrain.Capabilities;

namespace IntoChat.OS.Modules.AI.Context;

/// <summary>
/// Canonical execution context bridging Orleans grain turn, behavior sandbox (AmbientSession),
/// security provenance (CapabilityCaller), and prompt assembly slices.
/// </summary>
public interface IExecutionContext
{
    /// <summary>Unique identifier for the current execution turn.</summary>
    string ExecutionId { get; }

    /// <summary>Security provenance of the caller executing this turn, if stamped.</summary>
    CapabilityCaller? Caller { get; }

    /// <summary>Read-only environment and configuration settings.</summary>
    IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>Active context slices contributing to model prompt assembly.</summary>
    IReadOnlyList<ContextSlice> Slices { get; }

    /// <summary>Cancellation token governing this execution turn.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Executes an asynchronous delegate within a scoped context containing an additional slice.
    /// </summary>
    Task<TResult> WithSliceAsync<TResult>(ContextSlice slice, Func<Task<TResult>> action);
}
