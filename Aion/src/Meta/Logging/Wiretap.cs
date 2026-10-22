using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Aion.Meta.Logging.Wiretap;

public static class Wiretap
{
    public const string AppName = "aion";
}

public record LoggerProperty(string Name, object? Value)
{
    public static implicit operator (string Name, object? Value)(LoggerProperty state) => (state.Name, state.Value);
}

//public record Activity(string Name) : LoggerProperty("aion.activity", Name);

public record Story(object? Value) : LoggerProperty($"{Wiretap.AppName}.activity.story", Value)
{
    public static Story User { get; } = new("user");
    public static Story System { get; } = new("system");
}

public record Event(object? Value) : LoggerProperty($"{Wiretap.AppName}.activity.event", Value)
{
    // core: This is the very first event.
    public static Event Zero { get; } = new("zero");

    public sealed record Last(object? Value, System.Diagnostics.ActivityStatusCode StatusCode) : Event(Value)
    {
        public static Last Okay { get; } = new("okay", System.Diagnostics.ActivityStatusCode.Ok);
        public static Last Fail { get; } = new("fail", System.Diagnostics.ActivityStatusCode.Error);
        public static Last Noop { get; } = new("noop", System.Diagnostics.ActivityStatusCode.Ok);
        public static Last Halt { get; } = new("halt", System.Diagnostics.ActivityStatusCode.Ok);
        public static Last Auto { get; } = new("auto", System.Diagnostics.ActivityStatusCode.Unset);

        public static Last Map(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Error ? Fail : Okay;
        }
    }
}

public interface IPropertyLogger : ILogger
{
    public ILogger Logger { get; }
}

public class PropertyLogger(ILogger logger, LoggerProperty property) : IPropertyLogger
{
    public ILogger Logger => logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Resolve last before IsEnabled, so scope-disposal does not think it's empty.
        var logEvent = property;
        if (property is Event.Last last)
        {
            // core: Override the last event if it's set to auto.
            if (last == Event.Last.Auto)
            {
                last = Event.Last.Map(logLevel);
            }

            if (this.Scope is { } scope)
            {
                scope.SetLast(last);
            }

            logEvent = last;
        }

        if (IsEnabled(logLevel))
        {
            using (logger.BeginScopeWith(logEvent))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public interface IActivityLogger : ILogger, IDisposable
{
    bool SetLast(Event.Last last);
}

public delegate void ConfigureActivity(System.Diagnostics.Activity activity);

public class ActivityLogger
(
    string name,
    ConfigureActivity configure,
    ILogger logger,
    IDisposable? scope
) : IActivityLogger
{
    private System.Diagnostics.Activity Activity { get; } = CreateActivity(name, configure);

    private Event.Last? LastEvent { get; set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using (logger.BeginScopeWith(($"{Wiretap.AppName}.activity.name", Activity.OperationName)))
            {
                if (LastEvent is not null)
                {
                    using (logger.BeginScopeWith(($"{Wiretap.AppName}.activity.duration_ms", (long)Activity.Duration.TotalMilliseconds)))
                    {
                        logger.Log(logLevel, eventId, state, exception, formatter);
                    }
                }
                else
                {
                    logger.Log(logLevel, eventId, state, exception, formatter);
                }
            }
        }
    }

    public bool SetLast(Event.Last last)
    {
        if (LastEvent is null)
        {
            Activity.SetEndTime(DateTime.UtcNow);
            Activity.SetStatus(last.StatusCode);
            LastEvent = last;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (SetLast(Event.Last.Auto))
        {
            this.LogWarning("Activity scope '{" + Wiretap.AppName + ".activity.name}' was disposed without setting the last event.", Activity.OperationName);
        }

        Activity.Dispose();
        scope?.Dispose();
    }

    private static System.Diagnostics.Activity CreateActivity(string name, ConfigureActivity configure)
    {
        var activity = new System.Diagnostics.Activity(name);
        configure(activity);
        return activity.Start();
    }
}

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        // util: The built-in BeginScope does not support anonymous objects, thus this helper.
        public IDisposable? BeginScopeFrom<T>(T state) where T : notnull
        {
            var properties =
                from p in state.GetType().GetProperties()
                select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));

            return logger.BeginScope(properties);
        }

        public IDisposable? BeginScopeWith(params (string Name, object? Value)[] state)
        {
            return logger.BeginScope(state.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value)));
        }

        public IActivityLogger BeginActivity(string name, ConfigureActivity configure, params (string Name, object? Value)[] state)
        {
            return new ActivityLogger(name, configure, logger, logger.BeginScopeWith(state));
        }

        public IActivityLogger BeginActivity(string name, params (string Name, object? Value)[] state)
        {
            return logger.BeginActivity(name, _ => { }, state);
        }

        public IActivityLogger BeginActivity<TActivityName>(ConfigureActivity configure, params (string Name, object? Value)[] state)
        {
            return logger.BeginActivity(typeof(TActivityName).Name, configure, state);
        }

        public IActivityLogger BeginActivity<TActivityName>(params (string Name, object? Value)[] state)
        {
            return logger.BeginActivity(typeof(TActivityName).Name, _ => { }, state);
        }

        public IPropertyLogger With(LoggerProperty property) => new PropertyLogger(logger, property);

        public IPropertyLogger User => logger.With(Story.User);
        public IPropertyLogger System => logger.With(Story.System);
    }

    extension(IPropertyLogger propertyLogger)
    {
        // core: Locates the scope and sets the last event.
        public IActivityLogger? Scope
        {
            get
            {
                ILogger current = propertyLogger;
                while (current is IPropertyLogger decorator)
                {
                    current = decorator.Logger;
                }

                if (current is IActivityLogger scope)
                {
                    return scope;
                }

                return null;
            }
        }

        public IPropertyLogger Zero => propertyLogger.With(Event.Zero);
        public IPropertyLogger Okay => propertyLogger.With(Event.Last.Okay);
        public IPropertyLogger Fail => propertyLogger.With(Event.Last.Fail);
        public IPropertyLogger Noop => propertyLogger.With(Event.Last.Noop);
        public IPropertyLogger Halt => propertyLogger.With(Event.Last.Halt);
        public IPropertyLogger Last => propertyLogger.With(Event.Last.Auto);
    }
}