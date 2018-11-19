using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class MaxDays : ConstraintBase
    {
        public MaxDays(int id, int d, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
            D = d;
        }

        public readonly int D;

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var acc = 0u;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                acc = acc | s.GetTime(Classes[i]).Days;
            }

            var count = Utilities.BitCount(acc);
            if (count <= D)
            {
                return (0d, 0);
            }

            count -= D;
            return Required
                ? (count, 0)
                : (0d, count * Penalty);
        }
    }
}
