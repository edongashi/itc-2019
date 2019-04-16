using Timetabling.Internal.Specialized;

namespace Timetabling.Internal.Constraints
{
    public class SameWeeks : TimeConstraint
    {
        public SameWeeks(int id, bool required, int penalty, int[] classes)
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
                    var orweeks = ci.Weeks | cj.Weeks;
                    if (orweeks == ci.Weeks || orweeks == cj.Weeks)
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
