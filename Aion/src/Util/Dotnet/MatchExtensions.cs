using System;
using System.Text.RegularExpressions;

namespace Aion.Util.Dotnet;

public static class MatchExtensions
{
    public static T GroupValueOrDefault<T>(this Match match, string groupName, Func<string, T> transform, T defaultValue)
    {
        var group = match.Groups[groupName];
        return group.Success ? transform(group.Value) : defaultValue;
    }
}