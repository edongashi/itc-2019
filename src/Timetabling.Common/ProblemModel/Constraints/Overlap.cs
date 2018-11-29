using Timetabling.Common.ProblemModel.Constraints.Internal;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class Overlap : TimeConstraint
    {
        public Overlap(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, Schedule[] configuration)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = configuration[i];
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = configuration[j];
                    if (cj.Start < ci.End
                        && ci.Start < cj.End
                        && (ci.Days & cj.Days) != 0u
                        && (ci.Weeks & cj.Weeks) != 0u)
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
