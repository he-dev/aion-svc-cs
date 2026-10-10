using System.Collections.Generic;
using System.Linq;

namespace Aion.Meta;

public static class DictionaryExtensions
{
    public static IEnumerable<KeyValuePair<TKey, TValue>> KeepLast<TKey, TValue>
    (
        this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs,
        IEqualityComparer<TKey>? comparer = null
    ) where TKey : notnull
    {
        return keyValuePairs.GroupBy(x => x.Key,  (x, y) => y.Last(), comparer);
    }
}