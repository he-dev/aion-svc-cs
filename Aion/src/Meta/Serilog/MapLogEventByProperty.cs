using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace Aion.Meta.Serilog;

// core: This sink redirects log events into the custom logger.
public class MapLogEventByProperty(string propertyName) : ILogEventSink
{
    public string PropertyName => propertyName;

    private ConcurrentDictionary<ActivitySpanId, ILogger> Loggers { get; } = new();

    public Registration To(ILogger register)
    {
        // core: Make the caller remove it when done.
        var registration = new Registration(executionId =>
        {
            // meta: Serilog recommends disposing of the logger this way.
            if (Loggers.TryRemove(executionId, out var logger) && logger is IDisposable disposable)
            {
                disposable.Dispose();
                global::Serilog.Debugging.SelfLog.WriteLine("Workflow logger for execution '{0}' was removed.", executionId);
            }
        });

        // core: Register the new logger.
        if (!Loggers.TryAdd(registration.ExecutionId, register))
        {
             global::Serilog.Debugging.SelfLog.WriteLine("Workflow logger for execution '{0}' is already registered.", registration.ExecutionId);
        }

        return registration;
    }

    public void Emit(LogEvent logEvent)
    {
        // core: The workflow logger is allowed to log only events with the workflow-execution-ID that is the same as this one.
        if (logEvent.TryGetScalar<string>(PropertyName, out var scalar))
        {
            var executionId = ActivitySpanId.CreateFromString(scalar);

            // core: Redirect log events to the custom logger when the signature matches.
            if (Loggers.TryGetValue(executionId, out var logger))
            {
                logger.Write(logEvent);
            }
        }
    }

    // meta: Discards the logger as it's no logger necessary.
    public class Registration(Action<ActivitySpanId> dispose) : IDisposable
    {
        public ActivitySpanId ExecutionId { get; init; } = ActivitySpanId.CreateRandom();

        public void Dispose() => dispose(ExecutionId);
    }
}