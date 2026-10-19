using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Aion.Meta.Serilog;

public sealed class ObservableSink : ILogEventSink, IObservable<LogEvent>
{
    private const byte Dummy = 0;

    // note: Uses a ConcurrentDictionary to avoid locking, because there is no ConcurrentHashSet.
    private ConcurrentDictionary<IObserver<LogEvent>, byte> Observers { get; } = new(ReferenceEqualityComparer.Instance);

    public void Emit(LogEvent logEvent)
    {
        foreach (var (observer, _) in Observers)
        {
            observer.OnNext(logEvent);
        }
    }

    public IDisposable Subscribe(IObserver<LogEvent> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        return
            Observers.TryAdd(observer, Dummy)
                ? new Subscription(() => Observers.TryRemove(observer, out _))
                // core: Duplicate subscription is not allowed.
                : throw new InvalidOperationException("Observer is already subscribed.");
    }

    private class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}

public class LogEventObserver : IObserver<LogEvent>, IEnumerable<LogEvent>, IDisposable
{
    private const string PropertyName = "meta.observable.id";

    private ConcurrentQueue<LogEvent> Queue { get; } = new();

    private Guid Id { get; } = Guid.NewGuid();

    private IDisposable? Scope { get; }

    public LogEventObserver(Func<IDictionary<string, object>, IDisposable?> beginScope)
    {
        Scope = beginScope(new Dictionary<string, object>
        {
            [PropertyName] = Id.ToString()
        });
    }

    public void OnCompleted() { }

    public void OnError(Exception error) { }

    public void OnNext(LogEvent logEvent)
    {
        if (logEvent.TryGetScalar<string>(PropertyName, out var scalar) && Guid.Parse(scalar).Equals(Id))
        {
            Queue.Enqueue(logEvent);
        }
    }

    public IEnumerator<LogEvent> GetEnumerator() => Queue.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Queue).GetEnumerator();

    public void Dispose() => Scope?.Dispose();
}

public static class LogEventObserverExtensions
{
    public static IEnumerable<LogEvent> AtLeastErrors(this LogEventObserver observer)
    {
        return observer.Where(e => e.Level >= LogEventLevel.Error);
    }

    public static IEnumerable<LogEvent> AtLeastWarnings(this LogEventObserver observer)
    {
        return observer.Where(e => e.Level >= LogEventLevel.Warning);
    }

    public static IEnumerable<string> Messages(this IEnumerable<LogEvent> events)
    {
        return events.Select(e => e.RenderMessage());
    }

    public static LogEventObserver BeginLogEventObserver(this ILogger logger)
    {
        return new LogEventObserver(logger.BeginScope);
    }
}