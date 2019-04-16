using Timetabling.Internal.Specialized;
using Timetabling.Internal.Utils;

namespace Timetabling.Internal.Constraints
{
    public class MaxDays : TimeConstraint
    {
        public MaxDays(int id, int d, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
            D = d;
        }

        public readonly int D;

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, Schedule[] configuration)
        {
            var acc = 0u;
            for (var i = 0; i < Classes.Length; i++)
            {
                acc = acc | configuration[i].Days;
            }

            var count = Utilities.BitCount(acc);
            if (count <= D)
            {
                return (0, 0);
            }

            count -= D;
            return Required
                ? (count, 0)
                : (0, count * Penalty);
        }
    }
}
