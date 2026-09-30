using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Reusable;

using Reusable.Exceptionize;
using Reusable.Extensions;

// ReSharper disable once CheckNamespace
namespace System.Linq.Custom
{
    [PublicAPI]
    // ReSharper disable once InconsistentNaming - I want this lower case because otherwise it conflicts with the .net class.
    public static class enumerable
    {
        /// <summary>
        /// Applies a specified function to the corresponding elements of two sequences, producing a sequence of the results.
        /// Unlike the default Zip method this one doesn't stop if enumerating one of the collections is complete.
        /// It continues enumerating the longer collection and return default(T) for the other one.
        /// </summary>
        /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
        /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
        /// <typeparam name="TResult">The type of the elements of the result sequence.</typeparam>
        /// <param name="first">The first input sequence.</param>
        /// <param name="second">The second input sequence.</param>
        /// <param name="resultSelector">A function that specifies how to combine the corresponding elements of the two sequences.</param>
        /// <returns>An IEnumerableOfT that contains elements of the two input sequences, combined by resultSelector.</returns>
        public static IEnumerable<TResult> ZipOrDefault<TFirst, TSecond, TResult>
        (
            this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            Func<TFirst, TSecond, TResult> resultSelector = default
        )
        {
            using var x = first.GetEnumerator();
            using var y = second.GetEnumerator();

            var xm = false;
            var ym = false;
            while ((xm = x.MoveNext()) | (ym = y.MoveNext()))
            {
                yield return resultSelector
                (
                    xm ? x.Current : default,
                    ym ? y.Current : default
                );
            }
        }

        public static IEnumerable<(TFirst, TSecond)> ZipOrDefault<TFirst, TSecond>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second)
        {
            return first.ZipOrDefault(second, (f, s) => (f, s));
        }

        public static bool NotIn<T>([CanBeNull] this T value, params T[] others)
        {
            return !value.In((IEnumerable<T>)others);
        }

        public static bool In<T>([CanBeNull] this T value, [NotNull] IEnumerable<T> others, [NotNull] IEqualityComparer<T> comparer)
        {
            return others.Contains(value, comparer);
        }

        public static bool In<T>([CanBeNull] this T value, [NotNull] IEnumerable<T> others)
        {
            return value.In(others, EqualityComparer<T>.Default);
        }

        public static bool In<T>([CanBeNull] this T value, params T[] others)
        {
            return value.In(others.AsEnumerable());
        }

        public static bool Empty<TSource>([NotNull] this IEnumerable<TSource> source)
        {
            return source.Any() == false;
        }

    }

}