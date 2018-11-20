using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameTime : ConstraintBase
    {
        public SameTime(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Time;

        public override (int hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = s.GetTime(Classes[i]);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = s.GetTime(Classes[j]);
                    if (ci.Start <= cj.Start && cj.End <= ci.End ||
                        cj.Start <= ci.Start && ci.End <= cj.End)
                    {
                        continue;
                    }

                    penalty++;
                }
            }

            return Required ? (penalty, 0) : (0, Penalty * penalty);
        }
    }
}
