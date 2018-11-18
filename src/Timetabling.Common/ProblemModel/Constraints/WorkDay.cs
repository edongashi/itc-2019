using System;
using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class WorkDay : ConstraintBase
    {
        public WorkDay(int s, bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
            S = s;
        }

        public readonly int S;

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = s.GetTime(Classes[i]);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = s.GetTime(Classes[j]);
                    if ((ci.Days & cj.Days) == 0u
                        || (ci.Weeks & cj.Weeks) == 0u
                        || Math.Max(ci.End, cj.End) - Math.Min(ci.Start, cj.Start) <= S)
                    {
                        continue;
                    }

                    penalty++;
                }
            }

            return Required ? (penalty, 0) : (0, penalty);
        }
    }
}
