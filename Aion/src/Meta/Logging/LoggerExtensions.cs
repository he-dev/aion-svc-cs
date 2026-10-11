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
}