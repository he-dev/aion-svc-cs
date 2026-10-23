using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Aion.Meta.Serilog;

public sealed class ForkLogging(IServiceProvider services) : ILogEventSink, IObservable<LogEvent>
{
    // note: Each observer registration owns its scope to reduce object nesting.
    private ConcurrentDictionary<IObserver<LogEvent>, LogEventForkScope> Bugs { get; } = new(ReferenceEqualityComparer.Instance);

    public void Emit(LogEvent logEvent)
    {
        foreach (var (_, scope) in Bugs)
        {
            scope.OnNext(logEvent);
        }
    }

    // note: Hides this method to use a more descriptive name.
    IDisposable IObservable<LogEvent>.Subscribe(IObserver<LogEvent> observer) => With(observer);

    public IDisposable With(IObserver<LogEvent> bug, string propertyName = "logging.forks")
    {
        ArgumentNullException.ThrowIfNull(bug);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        // meta: Resolve the logger after the logging pipeline has been initialized to avoid circular dependencies.
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ForkLogging>>();
        var scope = new LogEventForkScope(logger, bug, propertyName);

        // core: Duplicate subscription is not allowed.
        if (!Bugs.TryAdd(bug, scope))
        {
            scope.Dispose();
            throw new InvalidOperationException("Observer is already subscribed.");
        }

        return new Subscription(() =>
        {
            if (Bugs.TryRemove(bug, out var removedScope))
            {
                removedScope.Dispose();
            }
        });
    }

    private class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}

public class InMemoryFork : IObserver<LogEvent>, IEnumerable<LogEvent>
{
    private ConcurrentQueue<LogEvent> Queue { get; } = new();
    public void OnNext(LogEvent logEvent) => Queue.Enqueue(logEvent);
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public IEnumerator<LogEvent> GetEnumerator() => Queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Queue).GetEnumerator();
}

public sealed class LoggerFork(ILogger logger) : IObserver<LogEvent>
{
    public void OnNext(LogEvent logEvent) => logger.Write(logEvent);
    public void OnCompleted() { }
    public void OnError(Exception error) { }
}

// core: Preserve outer capture IDs because nested logger scopes would otherwise replace the same property's ID.
internal sealed class LogEventForkScope : IObserver<LogEvent>, IDisposable
{
    private string PropertyName { get; }

    // meta: Flow independent scope chains per property through async calls without sharing mutable state.
    private static AsyncLocal<ImmutableDictionary<string, ImmutableStack<string>>?> CurrentIds { get; } = new();

    private string Id { get; } = Guid.NewGuid().ToString("N");

    private ImmutableDictionary<string, ImmutableStack<string>> PreviousIds { get; }

    private IObserver<LogEvent> Fork { get; }

    private IDisposable? LoggerScope { get; }

    public LogEventForkScope(Microsoft.Extensions.Logging.ILogger logger, IObserver<LogEvent> fork, string propertyName)
    {
        Fork = fork;
        PropertyName = propertyName;
        PreviousIds = CurrentIds.Value ?? ImmutableDictionary<string, ImmutableStack<string>>.Empty;

        // core: Include outer observers so they also capture events from nested scopes.
        var ids = PreviousIds.GetValueOrDefault(PropertyName, ImmutableStack<string>.Empty).Push(Id);

        // util: Publish the IDs as a sequence under one stable log property name.
        LoggerScope = logger.BeginScope(new Dictionary<string, object>
        {
            [PropertyName] = ids.ToArray()
        });

        CurrentIds.Value = PreviousIds.SetItem(PropertyName, ids);
    }

    public void OnCompleted() => Fork.OnCompleted();

    public void OnError(Exception error) => Fork.OnError(error);

    public void OnNext(LogEvent logEvent)
    {
        if (Matches(logEvent))
        {
            Fork.OnNext(logEvent);
        }
    }

    private bool Matches(LogEvent logEvent)
    {
        return
            logEvent
                .Properties
                .TryGetValue(PropertyName, out var property)
            && property is SequenceValue ids
            && ids.Elements.Any(value => value is ScalarValue { Value: string id } && id == Id);
    }

    public void Dispose()
    {
        // core: Observer scopes must be disposed in reverse creation order.
        LoggerScope?.Dispose();

        // meta: Restore the enclosing observer context for subsequent scopes.
        CurrentIds.Value = PreviousIds;
    }
}

public static class LogEventForkExtensions
{
    public static IEnumerable<LogEvent> AtLeast(this IEnumerable<LogEvent> fork, Microsoft.Extensions.Logging.LogLevel minLogLevel)
    {
        return fork.Where(e => e.Level >= minLogLevel switch
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