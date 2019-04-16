using System;

namespace Timetabling.Internal.Specialized
{
    internal struct Block : IComparable<Block>, IEquatable<Block>
    {
        internal readonly int Start;
        internal readonly int End;

        internal Block(int start, int end)
        {
            Start = start;
            End = end;
        }

        internal int Length => End - Start;

        public int CompareTo(Block other)
        {
            return Start.CompareTo(other.Start);
        }

        internal void Deconstruct(out int start, out int end)
        {
            start = Start;
            end = End;
        }

        public bool Equals(Block other)
        {
            return Start == other.Start && End == other.End;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            return obj is Block block && Equals(block);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Start * 397) ^ End;
            }
        }
    }
}
