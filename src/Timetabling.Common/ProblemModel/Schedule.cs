namespace Timetabling.Common.ProblemModel
{
    public class Schedule
    {
        public Schedule(uint weeks, uint days, int start, int length)
        {
            Weeks = weeks;
            Days = days;
            Start = start;
            Length = length;
        }

        public readonly uint Weeks;

        public readonly uint Days;

        public readonly int Start;

        public readonly int Length;

        public int End => Start + Length;

        public bool IsBefore(Schedule other)
        {
            var w1 = Msb(Weeks);
            var w2 = Msb(other.Weeks);
            if (w1 > w2)
            {
                return true;
            }

            if (w1 != w2)
            {
                return false;
            }

            var d1 = Msb(Days);
            var d2 = Msb(other.Days);
            if (d1 > d2)
            {
                return true;
            }

            if (d1 != d2)
            {
                return false;
            }

            return Start + Length <= other.Start;
        }

        public bool Overlaps(Schedule other)
        {
            if ((Weeks & other.Weeks) == 0u)
            {
                return false;
            }

            if ((Days & other.Days) == 0u)
            {
                return false;
            }

            return Start < other.Start + other.Length && other.Start < Start + Length;
        }

        public bool Overlaps(Schedule other, int travelTime)
        {
            return true;
        }

        public override string ToString()
        {
            return $"({Start},{End})";
        }

        private static uint Msb(uint x)
        {
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x & ~(x >> 1);
        }
    }
}