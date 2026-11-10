using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Aion.Util.Serilog;

public sealed class ForkLogging : ILogEventSink, IObservable<LogEvent>
{
    // meta: AsyncLocal keeps subscriptions separate between awaits (async contexts).
    // meta: The ImmutableList lets an inner scope add a fork without changing the outer scope’s list.
    private readonly AsyncLocal<ImmutableList<Subscription>?> _subscriptions = new();

    private ImmutableList<Subscription> Subscriptions
    {
        get => _subscriptions.Value ?? ImmutableList<Subscription>.Empty;
        set => _subscriptions.Value = value;
    }

    // note: Hides this method to use a more descriptive name.
    IDisposable IObservable<LogEvent>.Subscribe(IObserver<LogEvent> observer) => Into(observer);

    public IDisposable Into(IObserver<LogEvent> fork)
    {
        ArgumentNullException.ThrowIfNull(fork);

        var subscriptions = Subscriptions;
        if (subscriptions.Any(subscription => ReferenceEquals(subscription.Fork, fork)))
        {
            throw new InvalidOperationException("Fork is already subscribed.");
        }

        var registration = new Subscription(fork, registration =>
        {
            Subscriptions = Subscriptions.Remove(registration, ReferenceEqualityComparer.Instance);
        });

        // core: Process subscription by inner first.
        Subscriptions = subscriptions.Insert(0, registration);
        return registration;
    }

    public void Emit(LogEvent logEvent)
    {
        foreach (var subscription in Subscriptions)
        {
            subscription.Fork?.OnNext(logEvent);
        }
    }

    private sealed class Subscription(IObserver<LogEvent> observer, Action<Subscription> unsubscribe) : IDisposable
    {
        private IObserver<LogEvent>? _observer = observer;

        public IObserver<LogEvent>? Fork => Volatile.Read(ref _observer);

        public void Dispose()
        {
            Volatile.Write(ref _observer, null);
            unsubscribe(this);
        }
    }
}

/// <summary>Forks logging into an in-memory collection.</summary>
public class InMemoryFork : IObserver<LogEvent>, IEnumerable<LogEvent>
{
    private ConcurrentQueue<LogEvent> Queue { get; } = new();
    public void OnNext(LogEvent logEvent) => Queue.Enqueue(logEvent);
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public IEnumerator<LogEvent> GetEnumerator() => Queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Queue).GetEnumerator();
}

/// <summary>Forks logging into the specified logger.</summary>
public sealed class LoggerFork(ILogger logger) : IObserver<LogEvent>
{
    public void OnNext(LogEvent logEvent) => logger.Write(logEvent);
    public void OnCompleted() { }
    public void OnError(Exception error) { }
}

public static class LogEventForkExtensions
{
    public static IEnumerable<LogEvent> AtLeast(this IEnumerable<LogEvent> events, Microsoft.Extensions.Logging.LogLevel minLogLevel)
    {
        return events.Where(e => e.Level >= minLogLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => LogEventLevel.Verbose,
            Microsoft.Extensions.Logging.LogLevel.Debug => LogEventLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => LogEventLevel.Information,
            Microsoft.Extensions.Logging.LogLevel.Warning => LogEventLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => LogEventLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => LogEventLevel.Fatal,
            Microsoft.Extensions.Logging.LogLevel.None => (LogEventLevel)int.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(minLogLevel), minLogLevel, null)
        });
    }

    public static IEnumerable<LogEvent> WhereProperty<T>(this IEnumerable<LogEvent> events, string name, T value)
    {
        return events.Where(e => e.TryGetScalar<T>(name, out var scalar) && EqualityComparer<T>.Default.Equals(scalar, value));
    }

    public static T? GetScalarOrDefault<T>(this LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var property) && property is ScalarValue { Value: T scalar })
        {
            return scalar;
        }

        return default;
    }

    public static IEnumerable<string> Format(this IEnumerable<LogEvent> events, ITextFormatter formatter)
    {
        return events.Select(e =>
        {
            using var writer = new StringWriter();
            formatter.Format(e, writer);
            return writer.ToString();
        });
    }

    public static IEnumerable<string> Format(this IEnumerable<LogEvent> events, string template, IFormatProvider? formatProvider = null)
    {
        return events.Format(new MessageTemplateTextFormatter(template, formatProvider));
    }
}
