using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace IntoChat.OS.ServiceDefaults;

// A process-local exporter barrier, enabled only by the test host's private capability.
// Callers must establish domain completion first; this does not wait for running work.
public static class TelemetryExportBarrier
{
    public const string CapabilityEnvironmentVariable = "INTOCHATOS_TEST_TELEMETRY_CAPABILITY";
    public const string CapabilityHeader = "X-IntoChatOS-Test-Telemetry";
    public const string Path = "/_testing/telemetry/flush";
    public const string SourceName = "IntoChat.OS.Testing.Telemetry";
    public const string MarkerName = "intochatos.telemetry.barrier";
    public const string MarkerAttribute = "intochatos.telemetry.barrier.id";
    private static readonly ActivitySource Source = new(SourceName);
    private const int FlushTimeoutMilliseconds = 30_000;

    public static WebApplication MapTelemetryExportBarrier(this WebApplication app)
    {
        if (app.Configuration[CapabilityEnvironmentVariable] is not { Length: > 0 } capability) { return app; }
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(capability));
        app.MapPost(Path, (HttpContext context) =>
        {
            var supplied = context.Request.Headers[CapabilityHeader];
            if (supplied.Count != 1 || !CryptographicOperations.FixedTimeEquals(expected,
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied[0] ?? ""))))
            { return Results.Unauthorized(); }

            var traces = app.Services.GetService<TracerProvider>();
            var logs = app.Services.GetServices<LoggerProvider>().ToArray();
            if (traces is null || logs.Length == 0) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
            var marker = Guid.NewGuid();
            // End the marker before flushing. Dashboard observers wait for both this span and log.
            using (var activity = Source.StartActivity(MarkerName, ActivityKind.Internal,
                new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)))
            {
                if (activity is null || !activity.Recorded) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
                activity.SetTag(MarkerAttribute, marker.ToString("D"));
            }
            app.Logger.LogWarning("Telemetry export barrier {TelemetryBarrierMarker}", marker.ToString("D"));
            var elapsed = Stopwatch.StartNew();
            try
            {
                if (!traces.ForceFlush(FlushTimeoutMilliseconds)) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
                foreach (var provider in logs)
                {
                    var remaining = FlushTimeoutMilliseconds - (int)elapsed.ElapsedMilliseconds;
                    if (remaining <= 0 || !provider.ForceFlush(remaining)) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
                }
                return Results.Ok(new TelemetryExportReceipt(marker, DateTimeOffset.UtcNow));
            }
            catch (Exception error)
            {
                // The HTTP result stays a status code. The exception reaches the log, not the response.
                app.Logger.LogDebug(error, "Telemetry export barrier flush failed.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous(); // The per-run capability above authenticates this opt-in endpoint.
        return app;
    }
}

public sealed record TelemetryExportReceipt(Guid Marker, DateTimeOffset CompletedAt);
