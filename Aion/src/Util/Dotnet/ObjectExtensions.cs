using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Aion.Util.Dotnet;

public static class ObjectExtensions
{
    // util: Caches the properties of a type.
    private static ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> Properties { get; } = new();

    public static bool TryGetProperty(this object source, string name, [MaybeNullWhen(false)] out PropertyInfo propertyInfo)
    {
        var properties = Properties.GetOrAdd(source.GetType(), key =>
        {
            return key
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        });

        if (!properties.TryGetValue(name.Trim(), out var pi))
        {
            propertyInfo = null;
            return false;
        }

        propertyInfo = pi;
        return true;
    }
}