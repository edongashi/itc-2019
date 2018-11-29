using Timetabling.Common.ProblemModel.Constraints.Internal;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameTime : TimeConstraint
    {
        public SameTime(int id, bool required, int penalty, int[] classes)
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
