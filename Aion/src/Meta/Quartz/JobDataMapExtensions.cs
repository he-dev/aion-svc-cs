using System;
using System.Collections.Generic;
using Quartz;

namespace Aion.Meta.Quartz;

public static class JobDataMapExtensions
{
    public static T Get<T>(this JobDataMap data, string? key = null)
    {
        key ??= typeof(T).Name;

        if (!data.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"The JobDataMap does not contain the key '{key}'.");
        }

        if (value is null)
        {
            throw new InvalidOperationException($"The JobDataMap contains the key '{key}', but its value is null.");
        }

        if (value is not T typedValue)
        {
            throw new InvalidCastException($"The JobDataMap value for key '{key}' has type '{value.GetType().FullName}', but type '{typeof(T).FullName}' was expected.");
        }

        return typedValue;
    }
}
