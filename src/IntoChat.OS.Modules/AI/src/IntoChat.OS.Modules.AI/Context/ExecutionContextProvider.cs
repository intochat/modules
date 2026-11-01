using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    internal const string ExecutionIdRequestKey = "db.exec.id";
    internal const string EnvironmentRequestKey = "db.exec.env";
    internal const string SlicesRequestKey = "db.exec.slices";

    private readonly AsyncLocal<IExecutionContext?> _explicit = new();
    private readonly AsyncLocal<string?> _defaultExecutionId = new();

    public IExecutionContext Current
    {
        get
        {
            if (_explicit.Value is { } explicitContext)
            {
                return explicitContext;
            }

            var execId = _defaultExecutionId.Value;
            if (string.IsNullOrWhiteSpace(execId))
            {
                execId = Orleans.Runtime.RequestContext.Get(ExecutionIdRequestKey) as string
                    ?? Guid.NewGuid().ToString("N");
                _defaultExecutionId.Value = execId;
            }

            return CreateDefault(execId);
        }
    }

    public IDisposable PushContext(IExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _explicit.Value;
        var prevId = Orleans.Runtime.RequestContext.Get(ExecutionIdRequestKey);
        var prevEnv = Orleans.Runtime.RequestContext.Get(EnvironmentRequestKey);
        var prevSlices = Orleans.Runtime.RequestContext.Get(SlicesRequestKey);

        _explicit.Value = context;
        Orleans.Runtime.RequestContext.Set(ExecutionIdRequestKey, context.ExecutionId);
        Orleans.Runtime.RequestContext.Set(EnvironmentRequestKey, context.Environment);
        if (context.Slices.Count > 0)
        {
            Orleans.Runtime.RequestContext.Set(SlicesRequestKey, ImmutableList.CreateRange(context.Slices));
        }

        return new Restore(() =>
        {
            _explicit.Value = previous;
            RestoreOrRemove(ExecutionIdRequestKey, prevId);
            RestoreOrRemove(EnvironmentRequestKey, prevEnv);
            RestoreOrRemove(SlicesRequestKey, prevSlices);
        });
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

    private static IExecutionContext CreateDefault(string execId)
    {
        // Bridge 1: Check Behavior Sandbox (AmbientSession)
        var (ambientSettings, ambientCt) = TryReadAmbientSession();

        // Bridge 2: Check Security Provenance (CallerContextStamper / RuntimeCallerContext)
        var caller = CallerContextStamper.TryGet(out var stamped)
            ? stamped
            : RuntimeCallerContext.Current;

        // Bridge 3: Environment Settings (Orleans RequestContext + AmbientSession)
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Orleans.Runtime.RequestContext.Get(EnvironmentRequestKey) is IReadOnlyDictionary<string, string> propagatedEnv)
        {
            foreach (var (k, v) in propagatedEnv)
            {
                env[k] = v;
            }
        }

        if (ambientSettings != null)
        {
            foreach (var (k, v) in ambientSettings)
            {
                env[k] = v;
            }
        }

        var slices = Orleans.Runtime.RequestContext.Get(SlicesRequestKey) as IEnumerable<ContextSlice>;
        var ct = ambientCt ?? CancellationToken.None;

        return new ExecutionContext(execId, caller, env, slices, ct);
    }

    private static void RestoreOrRemove(string key, object? value)
    {
        if (value is null)
        {
            Orleans.Runtime.RequestContext.Remove(key);
        }
        else
        {
            Orleans.Runtime.RequestContext.Set(key, value);
        }
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
