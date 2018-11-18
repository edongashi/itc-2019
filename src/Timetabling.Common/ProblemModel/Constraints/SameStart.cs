using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameStart : ConstraintBase
    {
        public SameStart(bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ti = s.GetTime(Classes[i]).Start;
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var tj = s.GetTime(Classes[j]).Start;
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
