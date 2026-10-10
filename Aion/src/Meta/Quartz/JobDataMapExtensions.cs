using System;
using System.Collections.Generic;
using Quartz;

namespace Aion.Meta.Quartz;

public static class JobDataMapExtensions
{
    public static T Get<T>(this JobDataMap data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is null)
            {
                throw new NullReferenceException($"The {nameof(JobDataMap)} contains a null value at {key}.");
            }

            return (T)value;
        }

        throw new KeyNotFoundException($"The code is trying to get the value for the {key} key, but it does not exist in the {nameof(JobDataMap)}.");
    }


    public static T GetEnum<T>(this JobDataMap map) where T : struct, Enum
    {
        return
            map.GetString(typeof(T).Name) is { } value
                ? Enum.Parse<T>(value)
                : throw new KeyNotFoundException($"The code is trying to get the value by the enum's type name '{typeof(T).Name}', but such key does not exist in the {nameof(JobDataMap)}.");
    }
}