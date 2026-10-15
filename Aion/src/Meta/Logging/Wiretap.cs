using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Aion.Meta.Logging;

public record Pulse(string Value)
{
    public string Name => "wiretap.pulse";

    public static Pulse Core { get; } = new("core");
    public static Pulse Buzz { get; } = new("buzz");

    public static implicit operator (string Name, object? Value)(Pulse state) => (state.Name, state.Value);
}

public record Event(string Value)
{
    public string Name => "wiretap.event";

    // core: This is the very first event.
    public static Event Zero { get; } = new("zero");

    public sealed record Last(string Value) : Event(Value)
    {
        public static Last Okay { get; } = new("okay");
        public static Last Fail { get; } = new("fail");
        public static Last Noop { get; } = new("noop");
        public static Last Void { get; } = new("void");
    }

    public static implicit operator (string Name, object? Value)(Event state) => (state.Name, state.Value);
}

public interface IWiretap : ILogger
{
    public ILogger Logger { get; }
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

public interface IWiretapScope : ILogger, IDisposable
{
    void SetLast(Event.Last last);
}

public class WiretapScope(ILogger logger, IDisposable? scope, string activity) : IWiretapScope
{
    private static readonly Dictionary<Event.Last, System.Diagnostics.ActivityStatusCode> StatusByEvent = new()
    {
        [Event.Last.Okay] = System.Diagnostics.ActivityStatusCode.Ok,
        [Event.Last.Noop] = System.Diagnostics.ActivityStatusCode.Ok,
        [Event.Last.Fail] = System.Diagnostics.ActivityStatusCode.Error
    };

    public ILogger Logger => logger;

    private System.Diagnostics.Activity Activity { get; } = new System.Diagnostics.Activity(activity).Start();

    private Event.Last? LastEvent { get; set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var isLast = LastEvent is not null;

            if (isLast)
            {
                Activity.SetEndTime(DateTime.UtcNow);

                if (StatusByEvent.TryGetValue(LastEvent!, out var status))
                {
                    Activity.SetStatus(status);
                }
            }

            using (logger.BeginScopeWith(("wiretap.activity", Activity.OperationName)))
            using (isLast ? logger.BeginScopeWith(("wiretap.duration_ms", Activity.Duration.TotalMilliseconds)) : null)
            {
                try
                {
                    logger.Log(logLevel, eventId, state, exception, formatter);
                }
                finally
                {
                    if (isLast)
                    {
                        Activity.Stop();
                    }
                }
            }
        }
    }

    public void SetLast(Event.Last last) => LastEvent = last;

    public void Dispose()
    {
        if (LastEvent is null)
        {
            LastEvent = Event.Last.Void;
            this.LogWarning("Wiretap scope '{wiretap.activity}' was disposed without setting the last event.", Activity.OperationName);
        }

        Activity.Dispose();
        scope?.Dispose();
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

        public IWiretapScope BeginScopeWith(string activity, params (string Name, object? Value)[] state)
        {
            var pairs = state.Append(("wiretap.activity", activity)).Select(p => new KeyValuePair<string, object?>(p.Name, p.Value));
            return new WiretapScope(logger, logger.BeginScope(pairs), activity);
        }

        public IWiretap With(params (string Name, object? Value)[] state) => new Wiretap(logger, state);

        public IWiretap Core => logger.With(Pulse.Core);
        public IWiretap Buzz => logger.With(Pulse.Buzz);
    }

    extension(IWiretap wiretap)
    {
        // core: Locates the scope and sets the last event.
        public Event.Last Last(Event.Last state)
        {
            ILogger current = wiretap;

            while (current is IWiretap decorator)
            {
                current = decorator.Logger;
            }

            if (current is IWiretapScope scope)
            {
                scope.SetLast(state);
            }

            return state;
        }

        public IWiretap Zero => wiretap.With(Event.Zero);

        public IWiretap Okay => wiretap.With(wiretap.Last(Event.Last.Okay));

        public IWiretap Fail => wiretap.With(wiretap.Last(Event.Last.Fail));

        public IWiretap Noop => wiretap.With(wiretap.Last(Event.Last.Noop));
    }
}