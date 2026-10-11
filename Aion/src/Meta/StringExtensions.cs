using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Aion.Meta;

public static class StringExtensions
{
    public static bool IsLike(this string text, string? pattern, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var matcher = new Matcher(comparison);
        matcher.AddInclude(pattern ?? "*");
        return matcher.Match(text).HasMatches;
    }

    public static string Join(this IEnumerable<string> values, string separator) => string.Join(separator, values);

    public static string ToSlash(this string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        return value.Replace('\\', '/');
    }
}