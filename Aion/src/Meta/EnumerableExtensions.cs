using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.Meta;

public static class EnumerableExtensions
{
    /// <summary>
    /// Tries to get the only element of a collection by throwing more specific exceptions that the built-in Single in case it was not possible.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="onEmpty"></param>
    /// <param name="onExtra"></param>
    public static T SingleOrThrows<T>(this IEnumerable<T> source, Func<Exception> onEmpty, Func<Exception> onExtra)
    {
        using var enumerator = source.GetEnumerator();

        // core: Try to advance to the first item.
        if (!enumerator.MoveNext())
        {
            throw onEmpty();
        }

        var first = enumerator.Current;

        // core: Try to advance to the second item.
        if (enumerator.MoveNext())
        {
            // Multiple matches.
            throw onExtra();
        }

        return first;
    }

    public static IEnumerable<KeyValuePair<TKey, TValue>> LastValueWins<TKey, TValue>
    (
        this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs,
        IEqualityComparer<TKey>? comparer = null
    ) where TKey : notnull
    {
        return keyValuePairs.GroupBy(x => x.Key,  (key, values) => values.Last(), comparer);
    }
}

public class CollectionEmptyException : Exception;

public class AmbiguousResultException : Exception;