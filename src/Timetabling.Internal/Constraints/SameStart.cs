using Timetabling.Internal.Specialized;

namespace Timetabling.Internal.Constraints
{
    public class SameStart : TimeConstraint
    {
        public SameStart(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, Schedule[] configuration)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ti = configuration[i].Start;
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var tj = configuration[j].Start;
                    if (ti == tj)
                    {
                        continue;
                    }

                    penalty++;
                }

                if (penalty == 0)
                {
                    break;
                }
            }

            return Required ? (penalty, 0) : (0, Penalty * penalty);
        }
    }
}
