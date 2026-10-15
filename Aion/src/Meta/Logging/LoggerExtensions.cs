using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Aion.Meta.Logging;

public static class LoggerExtensions
{
    // util: The built-in BeginScope does not support anonymous objects, thus this helper.
    public static IDisposable? BeginScopeFrom<T>(this ILogger logger, T state) where T : notnull
    {
        var properties =
            from p in state.GetType().GetProperties()
            select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));

        return logger.BeginScope(properties);
    }

    public static IDisposable? BeginScopeWith(this ILogger logger, params (string Name, object? Value)[] state)
    {
        return logger.BeginScope(state.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value)));
    }

    public static IWiretapScope BeginScopeWith(this ILogger logger, string activity, params (string Name, object? Value)[] state)
    {
        var pairs = state.Append(("wiretap.activity", activity)).Select(p => new KeyValuePair<string, object?>(p.Name, p.Value));
        return new WiretapScope(logger, logger.BeginScope(pairs), activity);
    }

    extension(ILogger logger)
    {
        public IWiretap With(params (string Name, object? Value)[] state) => new Wiretap(logger, state);

        public IWiretap Core => logger.With(Pulse.Core);
        public IWiretap Buzz => logger.With(Pulse.Buzz);
    }

    extension(IWiretap wiretap)
    {
        public ValueTuple<string, object?> Last(ValueTuple<string, object?> state)
        {
            ILogger current = wiretap;

            while (current is IWiretap decorator)
            {
                current = decorator.Logger;
            }

            if (current is IWiretapScope scope)
            {
                scope.Last(state);
            }

            return state;
        }

        public IWiretap Zero => wiretap.With(Event.Zero);

        public IWiretap Okay => wiretap.With(wiretap.Last(Event.Okay));

        public IWiretap Fail => wiretap.With(wiretap.Last(Event.Fail));

        public IWiretap Noop => wiretap.With(wiretap.Last(Event.Noop));
    }
}

public abstract class Pulse
{
    public static ValueTuple<string, object?> Core { get; } = ("wiretap.pulse", "core");
    public static ValueTuple<string, object?> Buzz { get; } = ("wiretap.pulse", "buzz");
}

public abstract class Event
{
    public static ValueTuple<string, object?> Zero { get; } = ("wiretap.event", "zero");
    public static ValueTuple<string, object?> Okay { get; } = ("wiretap.event", "okay");
    public static ValueTuple<string, object?> Fail { get; } = ("wiretap.event", "fail");
    public static ValueTuple<string, object?> Noop { get; } = ("wiretap.event", "noop");
}

public static class Activity__
{
    public static ValueTuple<string, object?> Is<TActivityName>() => ("wiretap.activity", typeof(TActivityName).Name);
}