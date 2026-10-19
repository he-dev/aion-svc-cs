using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Aion.Meta.Serilog;

public sealed class ObservableSink : ILogEventSink, IObservable<LogEvent>
{
    private const byte Dummy = 0;

    // util: Uses a ConcurrentDictionary as a thread-safe set of observers.
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
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}

public class LogEventObserver(Func<IDictionary<string, object>, IDisposable?> beginScope)
    : IObserver<LogEvent>, IEnumerable<LogEvent>, IDisposable
{
    private ConcurrentQueue<LogEvent> Queue { get; } = new();

    private LogEventObserverScope Scope { get; } = new(beginScope);

    public void OnCompleted() { }

    public void OnError(Exception error) { }

    public void OnNext(LogEvent logEvent)
    {
        if (Scope.Matches(logEvent))
        {
            Queue.Enqueue(logEvent);
        }
    }

    public IEnumerator<LogEvent> GetEnumerator() => Queue.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Queue).GetEnumerator();

    public void Dispose() => Scope.Dispose();
}

internal sealed class LogEventObserverScope : IDisposable
{
    private const string PropertyName = "meta.observable.scopes";

    // meta: Flows IDs through async calls without sharing mutable state between branches.
    private static AsyncLocal<ImmutableStack<string>?> CurrentIds { get; } = new();

    private string Id { get; } = Guid.NewGuid().ToString("N");

    private ImmutableStack<string>? PreviousIds { get; }

    private IDisposable? Scope { get; }

    public LogEventObserverScope(Func<IDictionary<string, object>, IDisposable?> beginScope)
    {
        ArgumentNullException.ThrowIfNull(beginScope);

        PreviousIds = CurrentIds.Value;

        // core: Include outer observers so they also capture events from nested scopes.
        var ids = (PreviousIds ?? ImmutableStack<string>.Empty).Push(Id);

        // util: Publish the IDs as a sequence under one stable log property name.
        Scope = beginScope(new Dictionary<string, object>
        {
            [PropertyName] = ids.ToArray()
        });

        CurrentIds.Value = ids;
    }

    public bool Matches(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue(PropertyName, out var property) && property is SequenceValue ids && ids.Elements.Any(value => value is ScalarValue { Value: string id } && id == Id);
    }

    public void Dispose()
    {
        // core: Observer scopes must be disposed in reverse creation order.
        Scope?.Dispose();

        // meta: Restore the enclosing observer context for subsequent scopes.
        CurrentIds.Value = PreviousIds;
    }
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

    public static IEnumerable<LogEvent> AtLeastInformations(this LogEventObserver observer)
    {
        return observer.Where(e => e.Level >= LogEventLevel.Information);
    }

    public static IEnumerable<string> Messages(this IEnumerable<LogEvent> events)
    {
        var formatter = new MessageTemplateTextFormatter("");
        return events.Select(e => e.RenderMessage());
    }

    public static LogEventObserver BeginLogEventObserver(this ILogger logger)
    {
        return new LogEventObserver(logger.BeginScope);
    }
}