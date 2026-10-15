using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aion.Meta.Logging;

public interface IWiretap : ILogger
{
    public ILogger Logger { get; }
}

public interface IWiretapScope : ILogger, IDisposable
{
    void Last(ValueTuple<string, object?> state);
}

public class WiretapScope(ILogger logger, IDisposable? scope, string activity) : IWiretapScope
{
    public ILogger Logger => logger;

    public Activity Activity { get; } = new Activity(activity).Start();

    private ValueTuple<string, object?> LastEvent { get; set; } = default;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var isLast = new HashSet<(string, object?)> { Event.Okay, Event.Noop, Event.Fail }.Contains(LastEvent);
            var activity = isLast ? Activity : null;

            if (isLast)
            {
                activity?.SetEndTime(DateTime.UtcNow);
                if (LastEvent == Event.Okay) activity?.SetStatus(ActivityStatusCode.Ok);
                if (LastEvent == Event.Noop) activity?.SetStatus(ActivityStatusCode.Ok);
                if (LastEvent == Event.Fail) activity?.SetStatus(ActivityStatusCode.Error);
            }

            using (isLast ? logger.BeginScopeWith(("wiretap.duration_ms", activity?.Duration.TotalMilliseconds)) : null)
            {
                try
                {
                    logger.Log(logLevel, eventId, state, exception, formatter);
                }
                finally
                {
                    activity?.Stop();
                }
            }
        }
    }

    public void Last(ValueTuple<string, object?> state)
    {
        LastEvent = state;
    }

    public void Dispose()
    {
        Activity.Dispose();
        scope?.Dispose();
    }
}

public class Wiretap(ILogger logger, params (string Name, object? Value)[] loggerState) : IWiretap
{
    public ILogger Logger => logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using (logger.BeginScopeWith(loggerState))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}