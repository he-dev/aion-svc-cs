using System.Diagnostics.CodeAnalysis;
using Serilog.Events;

namespace Aion.Util.Serilog;

public static class LogEventExtensions
{
    public static bool TryGetScalar<T>(this LogEvent logEvent, string name, [MaybeNullWhen(false)] out T value)
    {
        if (logEvent.Properties.TryGetValue(name, out var property) && property is ScalarValue { Value: T scalar })
        {
            value = scalar;
            return true;
        }

        value = default;
        return false;
    }
}