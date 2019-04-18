using System;

namespace Timetabling.Internal.Specialized
{
    public class ConstraintState
    {
        internal ConstraintState(
            int hardPenalty, 
            int softPenalty)
        {
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
            double normalized = 1d;
            Normalized = normalized;
            Normalized2 = Math.Pow(normalized, 2d);
        }

        public readonly int HardPenalty;

        public readonly int SoftPenalty;

        public readonly double Normalized;

        public readonly double Normalized2;
    }
}
