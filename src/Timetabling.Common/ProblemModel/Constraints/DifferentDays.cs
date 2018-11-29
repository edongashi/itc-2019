using Timetabling.Common.ProblemModel.Constraints.Internal;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class DifferentDays : TimeConstraint
    {
        public DifferentDays(int id, bool required, int penalty, int[] classes)
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
                    if ((ci.Days & cj.Days) == 0u)
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
