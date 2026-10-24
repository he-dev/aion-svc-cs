using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Aion.Meta.Logging.Wiretap;

public enum Event
{
    Zero,
    Tick,
    Last,
}

public interface IPropertyLogger : ILogger
{
    public ILogger Outer { get; }
}

public class StoryLogger(ILogger logger, string story) : IPropertyLogger
{
    public ILogger Outer => logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using (logger.BeginScopeWith(("story", story)))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public class EventLogger(ILogger logger, Event activityEvent) : IPropertyLogger
{
    public ILogger Outer => logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Try pushing the event when inside a scene, otherwise skip logging due to an invalid event sequence.
        if (this.ActivityLogger is { } activityLogger && !activityLogger.Push(activityEvent, logLevel))
        {
            return;
        }

        if (IsEnabled(logLevel))
        {
            using (logger.BeginScopeWith(("activity.event", activityEvent)))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public interface IPropertyScope : ILogger, IDisposable;

public class PropertyScope(ILogger logger, KeyValuePair<string, object?>[] properties) : IPropertyScope
{
    private IDisposable? Scope { get; } = logger.BeginScope(properties);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public void Dispose() => Scope?.Dispose();
}

public interface IActivityLogger : ILogger, IDisposable
{
    bool Push(Event next, LogLevel logLevel);
}

public class ActivityLogger(string about, (string Name, object? Value)[] properties, Action<System.Diagnostics.Activity> configure, ILogger outer) : IActivityLogger
{
    private System.Diagnostics.Activity Activity { get; } = CreateActivity(about, configure);

    private IDisposable? LoggerScope { get; } = outer.BeginScopeWith
    ([
        ("activity.about", about),
        // core: Apply tick-event by default.
        ("activity.event", Event.Tick),
        .. properties
    ]);

    private Event? RecentEvent { get; set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => outer.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => outer.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var durationScope =
                RecentEvent is Event.Last
                    ? outer.BeginScopeWith(("activity.duration_ms", (long)Activity.Duration.TotalMilliseconds))
                    : null;

            using (durationScope)
            {
                outer.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    public bool Push(Event next, LogLevel logLevel)
    {
        switch (next)
        {
            case Event.Zero when RecentEvent is null:
                RecentEvent = next;
                Activity.Start();
                return true;
            case Event.Tick when RecentEvent is Event.Zero or Event.Tick:
                RecentEvent = next;
                return true;
            case Event.Last when RecentEvent is Event.Zero or Event.Tick:
                RecentEvent = next;
                Activity.SetEndTime(DateTime.UtcNow);
                switch (logLevel)
                {
                    case LogLevel.None:
                        // core: Leave the status as-is.
                        break;
                    case LogLevel.Trace:
                    case LogLevel.Debug:
                    case LogLevel.Information:
                    case LogLevel.Warning:
                        Activity.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        Activity.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
                        break;
                }

                return true;
            default:
                this.System.LogWarning("Unexpected event '{wiretap.event.next}' following '{wiretap.event.recent}'.", next, RecentEvent);
                return false;
        }
    }

    public void Dispose()
    {
        switch (RecentEvent)
        {
            case null:
                // core: Establish proper order of events.
                Push(Event.Zero, LogLevel.None);
                Push(Event.Last, LogLevel.None);
                this.LogWarning("Activity has not been properly initialized with Event.Zero.");
                break;
            case Event.Zero or Event.Tick:
                // core: Establish proper order of events.
                Push(Event.Last, LogLevel.None);
                this.LogWarning("Activity has not been properly finalized with Event.Last.");
                break;
        }

        Activity.Dispose();
        LoggerScope?.Dispose();
    }

    private static System.Diagnostics.Activity CreateActivity(string name, Action<System.Diagnostics.Activity> configure)
    {
        var activity = new System.Diagnostics.Activity(name);
        configure(activity);
        return activity;
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

        public IPropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return new PropertyScope(logger, [.. properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value))]);
        }

        public IActivityLogger BeginScopeAbout
        (
            string about,
            (string Name, object? Value)[]? properties = null,
            Action<System.Diagnostics.Activity>? configure = null
        )
        {
            return new ActivityLogger(about, properties ?? [], configure ?? (_ => { }), logger);
        }

        public IActivityLogger BeginScopeAbout<TAbout>
        (
            (string Name, object? Value)[]? properties = null,
            Action<System.Diagnostics.Activity>? configure = null
        )
        {
            return logger.BeginScopeAbout(typeof(TAbout).Name, properties, configure);
        }

        //public IPropertyLogger With(LoggerProperty property) => new PropertyLogger(logger, property);

        public IPropertyLogger Domain => new StoryLogger(logger, "Domain");
        public IPropertyLogger System => new StoryLogger(logger, "System");
    }

    extension(IPropertyLogger propertyLogger)
    {
        // core: Locates the scene.
        public IActivityLogger? ActivityLogger
        {
            get
            {
                ILogger current = propertyLogger;
                while (current is IPropertyLogger decorator)
                {
                    current = decorator.Outer;
                }

                return current as IActivityLogger;
            }
        }

        public IPropertyLogger Zero => new EventLogger(propertyLogger, Event.Zero);
        public IPropertyLogger Tick => new EventLogger(propertyLogger, Event.Tick);
        public IPropertyLogger Last => new EventLogger(propertyLogger, Event.Last);
    }
}