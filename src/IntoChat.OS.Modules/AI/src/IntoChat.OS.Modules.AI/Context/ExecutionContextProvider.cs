using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DigitalBrain.Capabilities;
using DigitalBrain.Runtime.Orleans.Capabilities;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.AI.Context;

public interface IExecutionContextProvider
{
    IExecutionContext Current { get; }
    IDisposable PushContext(IExecutionContext context);
    IExecutionContext Create(
        string? executionId = null,
        CapabilityCaller? caller = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IEnumerable<ContextSlice>? initialSlices = null,
        CancellationToken cancellationToken = default);
}

public sealed class ExecutionContextProvider : IExecutionContextProvider
{
    private readonly AsyncLocal<IExecutionContext?> _current = new();

    public IExecutionContext Current
    {
        get
        {
            if (_current.Value is { } explicitContext)
            {
                return explicitContext;
            }

            var defaultContext = CreateDefault();
            _current.Value = defaultContext;
            return defaultContext;
        }
    }

    public IDisposable PushContext(IExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _current.Value;
        _current.Value = context;
        return new Restore(() => _current.Value = previous);
    }

    public IExecutionContext Create(
        string? executionId = null,
        CapabilityCaller? caller = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IEnumerable<ContextSlice>? initialSlices = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedCaller = caller
            ?? (CallerContextStamper.TryGet(out var stamped) ? stamped : RuntimeCallerContext.Current);

        return new ExecutionContext(executionId, resolvedCaller, environment, initialSlices, cancellationToken);
    }

    private static IExecutionContext CreateDefault()
    {
        // Bridge 1: Check Behavior Sandbox (AmbientSession)
        var (ambientSettings, ambientCt) = TryReadAmbientSession();

        // Bridge 2: Check Security Provenance (CallerContextStamper / RuntimeCallerContext)
        var caller = CallerContextStamper.TryGet(out var stamped)
            ? stamped
            : RuntimeCallerContext.Current;

        // Bridge 3: Environment Settings
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ambientSettings != null)
        {
            foreach (var (k, v) in ambientSettings)
            {
                env[k] = v;
            }
        }

        var ct = ambientCt ?? CancellationToken.None;
        var execId = Guid.NewGuid().ToString("N");

        return new ExecutionContext(execId, caller, env, null, ct);
    }

    private static (Dictionary<string, string>? Settings, CancellationToken? Stopping) TryReadAmbientSession()
    {
        try
        {
            var ambientType = Type.GetType("IntoChat.OS.Client.Runtime.AmbientSession, IntoChat.OS.Client");
            if (ambientType == null)
            {
                return (null, null);
            }

            var prop = ambientType.GetProperty("Value", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var scope = prop?.GetValue(null);
            if (scope == null)
            {
                return (null, null);
            }

            var scopeType = scope.GetType();
            var settingsProp = scopeType.GetProperty("Settings");
            var stoppingProp = scopeType.GetProperty("Stopping");

            var settings = settingsProp?.GetValue(scope) as Dictionary<string, string>;
            var stopping = stoppingProp?.GetValue(scope) as CancellationToken?;
            return (settings, stopping);
        }
        catch
        {
            return (null, null);
        }
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
