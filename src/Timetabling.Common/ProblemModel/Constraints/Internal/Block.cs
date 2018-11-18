using System;

namespace Timetabling.Common.ProblemModel.Constraints.Internal
{
    internal struct Block : IComparable<Block>, IEquatable<Block>
    {
        public readonly int Start;
        public readonly int End;

        public Block(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Length => End - Start;

        public int CompareTo(Block other)
        {
            return Start.CompareTo(other.Start);
        }

        public void Deconstruct(out int start, out int end)
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
