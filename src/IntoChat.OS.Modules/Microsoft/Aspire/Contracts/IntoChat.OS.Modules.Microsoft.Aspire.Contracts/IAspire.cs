using System.ComponentModel;
using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.Microsoft.Aspire;

public sealed record AspireResourceView(
    string Name,
    string State,
    string Endpoint);

public sealed record AspireLogEntry(
    DateTimeOffset Timestamp,
    string Resource,
    string Severity,
    string Message,
    string? Exception = null);

public sealed record AspireTraceSpan(
    string TraceId,
    string SpanId,
    string Resource,
    string Name,
    string Status,
    long DurationMicroseconds,
    string? Error = null);

[DigitalBrain.Signal("aspire.telemetry.changed")]
public sealed record AspireTelemetryChanged(int LogCount, int TraceCount) : DigitalBrain.Signal;

[NeuronAlias("aspire"), DefaultNeuronType("aspire")]
[Description("Local and cloud telemetry inspector for resources, console logs, structured logs, and distributed traces.")]
public interface IAspire
{
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<AspireResourceView[]> GetResourcesAsync();

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<string[]> GetConsoleLogsAsync(string resourceName, int tailLines = 50);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<AspireLogEntry[]> GetStructuredLogsAsync(string? resourceName = null, string? minSeverity = null);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<AspireTraceSpan[]> GetTracesAsync(string? resourceName = null, bool errorsOnly = false);

    Task RecordLogAsync(AspireLogEntry entry);

    Task RecordTraceAsync(AspireTraceSpan span);
}
