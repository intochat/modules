using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.Aspire;

[ExportableState]
public sealed record AspireTelemetryState(
    AspireResourceView[] Resources,
    AspireLogEntry[] StructuredLogs,
    AspireTraceSpan[] Traces)
{
    public AspireTelemetryState() : this(
        [
            new AspireResourceView("OperatingSystem", "Running", "http://localhost:5390"),
            new AspireResourceView("intochat-os-mcp", "Running", "http://localhost:5391"),
        ],
        [],
        [])
    {
    }
}

[DigitalBrain.Neuron("aspire", State = "aspire")]
public sealed class AspireNeuron : DigitalBrain.Neuron<AspireTelemetryState>, IAspire
{
    public Task<AspireResourceView[]> GetResourcesAsync()
        => Task.FromResult(State.Resources);

    public Task<string[]> GetConsoleLogsAsync(string resourceName, int tailLines = 50)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var lines = State.StructuredLogs
            .Where(log => string.Equals(log.Resource, resourceName, StringComparison.OrdinalIgnoreCase))
            .TakeLast(Math.Clamp(tailLines, 1, 500))
            .Select(log => $"[{log.Severity}] {log.Resource}: {log.Message}" + (log.Exception is null ? "" : $" ({log.Exception})"))
            .ToArray();
        return Task.FromResult(lines);
    }

    public Task<AspireLogEntry[]> GetStructuredLogsAsync(string? resourceName = null, string? minSeverity = null)
    {
        var query = State.StructuredLogs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            query = query.Where(log => string.Equals(log.Resource, resourceName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(minSeverity))
        {
            var minRank = SeverityRank(minSeverity);
            query = query.Where(log => SeverityRank(log.Severity) >= minRank);
        }

        return Task.FromResult(query.ToArray());
    }

    public Task<AspireTraceSpan[]> GetTracesAsync(string? resourceName = null, bool errorsOnly = false)
    {
        var query = State.Traces.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            query = query.Where(span => string.Equals(span.Resource, resourceName, StringComparison.OrdinalIgnoreCase));
        }

        if (errorsOnly)
        {
            query = query.Where(span => !string.IsNullOrWhiteSpace(span.Error) || string.Equals(span.Status, "Error", StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(query.ToArray());
    }

    public async Task RecordLogAsync(AspireLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var nextLogs = State.StructuredLogs.Append(entry).TakeLast(200).ToArray();
        var next = State with { StructuredLogs = nextLogs };
        await Save(next, new AspireTelemetryChanged(next.StructuredLogs.Length, next.Traces.Length));
    }

    public async Task RecordTraceAsync(AspireTraceSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        var nextTraces = State.Traces.Append(span).TakeLast(200).ToArray();
        var next = State with { Traces = nextTraces };
        await Save(next, new AspireTelemetryChanged(next.StructuredLogs.Length, next.Traces.Length));
    }

    private static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
    {
        "TRACE" or "DEBUG" => 1,
        "INFO" or "INFORMATION" => 2,
        "WARN" or "WARNING" => 3,
        "ERROR" => 4,
        "CRITICAL" or "FATAL" => 5,
        _ => 0,
    };
}

public sealed class FakeAspire : IAspire
{
    private readonly List<AspireResourceView> _resources =
    [
        new AspireResourceView("OperatingSystem", "Running", "http://localhost:5390"),
        new AspireResourceView("intochat-os-mcp", "Running", "http://localhost:5391"),
    ];
    private readonly List<AspireLogEntry> _logs = [];
    private readonly List<AspireTraceSpan> _traces = [];

    public Task<AspireResourceView[]> GetResourcesAsync() => Task.FromResult(_resources.ToArray());

    public Task<string[]> GetConsoleLogsAsync(string resourceName, int tailLines = 50)
    {
        var lines = _logs
            .Where(log => string.Equals(log.Resource, resourceName, StringComparison.OrdinalIgnoreCase))
            .TakeLast(Math.Clamp(tailLines, 1, 500))
            .Select(log => $"[{log.Severity}] {log.Resource}: {log.Message}" + (log.Exception is null ? "" : $" ({log.Exception})"))
            .ToArray();
        return Task.FromResult(lines);
    }

    public Task<AspireLogEntry[]> GetStructuredLogsAsync(string? resourceName = null, string? minSeverity = null)
    {
        var query = _logs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            query = query.Where(log => string.Equals(log.Resource, resourceName, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(query.ToArray());
    }

    public Task<AspireTraceSpan[]> GetTracesAsync(string? resourceName = null, bool errorsOnly = false)
    {
        var query = _traces.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            query = query.Where(span => string.Equals(span.Resource, resourceName, StringComparison.OrdinalIgnoreCase));
        }

        if (errorsOnly)
        {
            query = query.Where(span => !string.IsNullOrWhiteSpace(span.Error) || string.Equals(span.Status, "Error", StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(query.ToArray());
    }

    public Task RecordLogAsync(AspireLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _logs.Add(entry);
        return Task.CompletedTask;
    }

    public Task RecordTraceAsync(AspireTraceSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        _traces.Add(span);
        return Task.CompletedTask;
    }
}
