using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameRoom : ConstraintBase
    {
        public SameRoom(bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Room;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var hardPenalty = 0;
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

                    if (!Required)
                    {
                        return (0d, Penalty);
                    }

                    hardPenalty++;
                }

                if (hardPenalty == 0)
                {
                    break;
                }
            }

            return hardPenalty != 0 ? (hardPenalty, Penalty) : (0d, 0);
        }
    }
}
