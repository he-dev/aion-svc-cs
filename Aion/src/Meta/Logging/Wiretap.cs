using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Aion.Meta.Logging.Wiretap;

public record LoggerProperty(string Name, object? Value)
{
    public static implicit operator (string Name, object? Value)(LoggerProperty state) => (state.Name, state.Value);
}

public record Story(object? Value) : LoggerProperty("story", Value)
{
    public static Story Domain { get; } = new("domain");
    public static Story System { get; } = new("system");
}

public record Twist(object? Value) : LoggerProperty("scene.twist", Value)
{
    // core: This is the very first event.
    public static Twist Ready { get; } = new("ready");

    public sealed record Last(object? Value, System.Diagnostics.ActivityStatusCode StatusCode) : Twist(Value)
    {
        public static Last Yeah { get; } = new("yeah", System.Diagnostics.ActivityStatusCode.Ok);
        public static Last Oops { get; } = new("oops", System.Diagnostics.ActivityStatusCode.Error);
        public static Last Halt { get; } = new("halt", System.Diagnostics.ActivityStatusCode.Ok);
        public static Last Limbo { get; } = new("limbo", System.Diagnostics.ActivityStatusCode.Unset);

        public static Last Map(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Error ? Oops : Yeah;
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
        if (property is Twist.Last last)
        {
            // core: Override the last event if it's set to auto.
            if (last == Twist.Last.Limbo)
            {
                last = Twist.Last.Map(logLevel);
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

public interface ISceneLogger : ILogger, IDisposable
{
    bool SetLast(Twist.Last last);
}

public class SceneLogger(string about, (string Name, object? Value)[] properties, Action<System.Diagnostics.Activity> configure, ILogger logger) : ISceneLogger
{
    private System.Diagnostics.Activity Activity { get; } = CreateActivity(about, configure);

    private IDisposable? LoggerScope { get; } = logger.BeginScopeWith([("scene.about", about), .. properties]);

    private Twist.Last? LastEvent { get; set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            if (LastEvent is not null)
            {
                using (logger.BeginScopeWith(("scene.duration_ms", (long)Activity.Duration.TotalMilliseconds)))
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

    public bool SetLast(Twist.Last last)
    {
        if (LastEvent is null)
        {
            Activity.SetEndTime(DateTime.UtcNow);
            if (last.StatusCode > System.Diagnostics.ActivityStatusCode.Unset)
            {
                Activity.SetStatus(last.StatusCode);
            }

            LastEvent = last;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (SetLast(Twist.Last.Limbo))
        {
            this.LogWarning("Scene about '{scene.about}' was disposed without setting the last twist.", about);
        }

        Activity.Dispose();
        LoggerScope?.Dispose();
    }

    private static System.Diagnostics.Activity CreateActivity(string name, Action<System.Diagnostics.Activity> configure)
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

        public IPropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return new PropertyScope(logger, [.. properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value))]);
        }

        public ISceneLogger BeginScopeAbout(string about, (string Name, object? Value)[]? properties = null, Action<System.Diagnostics.Activity>? configure = null)
        {
            return new SceneLogger(about, properties ?? [], configure ?? (_ => { }), logger);
        }

        public ISceneLogger BeginScopeAbout<TAbout>((string Name, object? Value)[]? properties = null, Action<System.Diagnostics.Activity>? configure = null)
        {
            return logger.BeginScopeAbout(typeof(TAbout).Name, properties, configure);
        }

        public IPropertyLogger With(LoggerProperty property) => new PropertyLogger(logger, property);

        public IPropertyLogger Domain => logger.With(Story.Domain);
        public IPropertyLogger System => logger.With(Story.System);
    }

    extension(IPropertyLogger propertyLogger)
    {
        // core: Locates the scope and sets the last event.
        public ISceneLogger? Scope
        {
            get
            {
                ILogger current = propertyLogger;
                while (current is IPropertyLogger decorator)
                {
                    current = decorator.Logger;
                }

                if (current is ISceneLogger scope)
                {
                    return scope;
                }

                return null;
            }
        }

        public IPropertyLogger Ready => propertyLogger.With(Twist.Ready);
        public IPropertyLogger Yeah => propertyLogger.With(Twist.Last.Yeah);
        public IPropertyLogger Oops => propertyLogger.With(Twist.Last.Oops);
        public IPropertyLogger Halt => propertyLogger.With(Twist.Last.Halt);
        public IPropertyLogger Limbo => propertyLogger.With(Twist.Last.Limbo);
    }
}