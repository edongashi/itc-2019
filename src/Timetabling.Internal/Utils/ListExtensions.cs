using System;
using System.Collections.Generic;

namespace Timetabling.Internal.Utils
{
    internal static class ListExtensions
    {
        internal static T Peek<T>(this List<T> @this)
        {
            return @this[@this.Count - 1];
        }

        internal static T Pop<T>(this List<T> @this)
        {
            var result = @this[@this.Count - 1];
            @this.RemoveAt(@this.Count - 1);
            return result;
        }

        internal static void AddIfNotExists<T>(this List<T> @this, T item) where T : IEquatable<T>
        {
            if (!@this.Contains(item))
            {
                @this.Add(item);
            }
        }

        internal static void AddOrReplaceSorted<T>(this List<T> @this, T item) where T : IComparable<T>
        {
            if (@this.Count == 0)
            {
                @this.Add(item);
                return;
            }

            if (@this[@this.Count - 1].CompareTo(item) < 0)
            {
                @this.Add(item);
                return;
            }

            if (@this[0].CompareTo(item) > 0)
            {
                @this.Insert(0, item);
                return;
            }

            var index = @this.BinarySearch(item);
            if (index < 0)
            {
                @this.Insert(~index, item);
            }
            else
            {
                @this[index] = item;
            }
        }

        internal static void AddSorted<T>(this List<T> @this, T item) where T : IComparable<T>
        {
            if (@this.Count == 0)
            {
                @this.Add(item);
                return;
            }

            if (@this[@this.Count - 1].CompareTo(item) <= 0)
            {
                @this.Add(item);
                return;
            }

            if (@this[0].CompareTo(item) >= 0)
            {
                @this.Insert(0, item);
                return;
            }

            var index = @this.BinarySearch(item);
            if (index < 0)
            {
                index = ~index;
            }

            @this.Insert(index, item);
        }

        internal static int RemoveSorted<T>(this List<T> @this, T item) where T : IComparable<T>, IEquatable<T>
        {
            var index = @this.BinarySearch(item);
            if (index >= 0)
            {
                @this.RemoveAt(index);
                return index;
            }

            return -1;
        }

        internal static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var index = 0;
            foreach (var item in source)
            {
                if (predicate(item))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }
    }
}
