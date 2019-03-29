using System;
using Timetabling.Common.ProblemModel.Constraints.Internal;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class WorkDay : TimeConstraint
    {
        public WorkDay(int id, int s, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
            S = s;
        }

        public readonly int S;

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, Schedule[] configuration)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = configuration[i];
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = configuration[j];
                    if ((ci.Days & cj.Days) == 0u
                        || (ci.Weeks & cj.Weeks) == 0u
                        || Math.Max(ci.End, cj.End) - Math.Min(ci.Start, cj.Start) <= S)
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
