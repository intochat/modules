using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain.Capabilities;

namespace IntoChat.OS.Modules.AI.Context;

public sealed class ExecutionContext : IExecutionContext
{
    private readonly AsyncLocal<ImmutableList<ContextSlice>?> _scopedSlices = new();
    private readonly ImmutableList<ContextSlice> _initialSlices;

    public string ExecutionId { get; }
    public CapabilityCaller? Caller { get; }
    public IReadOnlyDictionary<string, string> Environment { get; }
    public CancellationToken CancellationToken { get; }

    public IReadOnlyList<ContextSlice> Slices => _scopedSlices.Value ?? _initialSlices;

    public ExecutionContext(
        string? executionId,
        CapabilityCaller? caller,
        IReadOnlyDictionary<string, string>? environment = null,
        IEnumerable<ContextSlice>? initialSlices = null,
        CancellationToken cancellationToken = default)
    {
        ExecutionId = string.IsNullOrWhiteSpace(executionId) ? Guid.NewGuid().ToString("N") : executionId;
        Caller = caller;
        Environment = environment != null
            ? new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _initialSlices = initialSlices != null
            ? [.. initialSlices]
            : ImmutableList<ContextSlice>.Empty;
        CancellationToken = cancellationToken;
    }

    public async Task<TResult> WithSliceAsync<TResult>(ContextSlice slice, Func<Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(action);

        var current = _scopedSlices.Value ?? _initialSlices;
        _scopedSlices.Value = current.Add(slice);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _scopedSlices.Value = current;
        }
    }
}
