using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameAttendees : ConstraintBase
    {
        public SameAttendees(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Common;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var penalty = 0;
            var problem = s.Problem;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var classi = Classes[i];
                var ci = s.GetTime(classi);
                var ri = s.GetRoom(classi);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var classj = Classes[j];
                    var cj = s.GetTime(classj);
                    var rj = s.GetRoom(classj);
                    var travel = ri != null && rj != null
                        ? problem.TravelTimes[ri.Id, rj.Id]
                        : 0;

                    if (ci.End + travel <= cj.Start
                        || cj.End + travel <= ci.Start
                        || (ci.Days & cj.Days) == 0u
                        || (ci.Weeks & cj.Weeks) == 0u)
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
