using System;
using System.Collections.Generic;

namespace Timetabling.Common
{
    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> @this, Action<T> action)
        {
            foreach (var item in @this)
            {
                action(item);
            }
        }

        public static T Tap<T>(this T @this, Action<T> action)
        {
            action(@this);
            return @this;
        }

        public static T Log<T>(this T @this, string message)
        {
            Console.WriteLine(message);
            return @this;
        }

        public static TResult Transform<T, TResult>(this T @this, Func<T, TResult> func)
        {
            return func(@this);
        }
    }
}
