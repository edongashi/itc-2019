using Timetabling.Common.ProblemModel.Constraints.Internal;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameAttendees : CommonConstraint
    {
        public SameAttendees(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, (Room room, Schedule schedule)[] configuration)
        {
            var penalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var (ri, ci) = configuration[i];
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var (rj, cj) = configuration[j];
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
