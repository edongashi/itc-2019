using System;
using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class MaxDayLoad : ConstraintBase
    {
        public MaxDayLoad(int id, int s, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
            S = s;
        }

        public readonly int S;

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var problem = s.Problem;
            var nrWeeks = problem.NumberOfWeeks;
            var nrDays = problem.DaysPerWeek;
            var sum = 0;
            for (var w = 0; w < nrWeeks; w++)
            {
                for (var d = 0; d < nrDays; d++)
                {
                    var dayLoad = 0;
                    // ReSharper disable once ForCanBeConvertedToForeach
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    for (var i = 0; i < Classes.Length; i++)
                    {
                        var ci = s.GetTime(Classes[i]);
                        if ((ci.Days & (1u << d)) != 0u
                            && (ci.Weeks & (1u << w)) != 0u)
                        {
                            dayLoad += ci.Length;
                        }
                    }

                    sum += Math.Max(dayLoad - S, 0);
                }
            }

            return Required
                ? ((double)sum / nrWeeks, Penalty * sum / nrWeeks)
                : (0d, Penalty * sum / nrWeeks);
        }
    }
}
