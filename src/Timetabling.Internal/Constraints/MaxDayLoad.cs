using System;
using Timetabling.Internal.Specialized;

namespace Timetabling.Internal.Constraints
{
    public class MaxDayLoad : TimeConstraint
    {
        public MaxDayLoad(int id, int s, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
            S = s;
        }

        public readonly int S;

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, Schedule[] configuration)
        {
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
                        var ci = configuration[i];
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
                ? (sum > 0 ? Math.Max(1, sum / nrWeeks) : 0, 0)
                : (0, Penalty * sum / nrWeeks);
        }
    }
}
