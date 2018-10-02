using System;

namespace Timetabling.Common.Utils
{
    public struct Override<T> : IEquatable<Override<T>>, IComparable<Override<T>>
    {
        public Override(int index, T value)
        {
            Index = index;
            Value = value;
        }

        public int Index { get; }

        public T Value { get; }

        public bool Equals(Override<T> other)
        {
            return Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return !(obj is null) && obj is Override<T> @override && Equals(@override);
        }

        public override int GetHashCode()
        {
            return Index;
        }

        public int CompareTo(Override<T> other)
        {
            return Index.CompareTo(other.Index);
        }

        public static implicit operator Override<T>(int index)
        {
            return new Override<T>(index, default);
        }

        public static implicit operator Override<T>((int index, T value) t)
        {
            return new Override<T>(t.index, t.value);
        }
    }
}
