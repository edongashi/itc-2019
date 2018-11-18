using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class DifferentRoom : ConstraintBase
    {
        public DifferentRoom(bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Room;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = s.GetRoom(Classes[i]);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = s.GetRoom(Classes[j]);
                    if (ci != cj)
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
