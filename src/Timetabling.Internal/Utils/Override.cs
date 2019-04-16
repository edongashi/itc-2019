using System;

namespace Timetabling.Internal.Utils
{
    internal struct Override<T> : IEquatable<Override<T>>, IComparable<Override<T>>
    {
        internal Override(int index, T value)
        {
            Index = index;
            Value = value;
        }

        internal int Index { get; }

        internal T Value { get; }

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
