using System.Collections.Generic;
using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameAttendees : CommonConstraint
    {
        public SameAttendees(int id, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
        }

        public override IEnumerable<int> EvaluateConflictingClasses(ISolution solution)
        {
            var cls = new HashSet<int>();
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var clsi = Classes[i];
                var ri = solution.GetRoom(clsi);
                var ci = solution.GetTime(clsi);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var clsj = Classes[j];
                    var rj = solution.GetRoom(clsj);
                    var cj = solution.GetTime(clsj);
                    var travel = ri != null && rj != null
                        ? solution.Problem.TravelTimes[ri.Id, rj.Id]
                        : 0;

                    if (ci.End + travel <= cj.Start
                        || cj.End + travel <= ci.Start
                        || (ci.Days & cj.Days) == 0u
                        || (ci.Weeks & cj.Weeks) == 0u)
                    {
                        continue;
                    }

                    cls.Add(i);
                    cls.Add(j);
                }
            }

            return cls;
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
