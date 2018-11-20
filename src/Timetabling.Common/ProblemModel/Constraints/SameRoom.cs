using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameRoom : ConstraintBase
    {
        public SameRoom(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Room;

        public override (int hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = s.GetRoomId(Classes[i]);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = s.GetRoomId(Classes[j]);
                    if (ci == cj)
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
