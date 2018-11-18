using System;
using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class SameDays : ConstraintBase
    {
        public SameDays(bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
        }

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var hardPenalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = s.GetTime(Classes[i]);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = s.GetTime(Classes[j]);
                    var ordays = ci.Days | cj.Days;
                    if (ordays == ci.Days || ordays == cj.Days)
                    {
                        continue;
                    }

                    if (!Required)
                    {
                        return (0d, Penalty);
                    }

                    hardPenalty++;
                }
            }

            return hardPenalty != 0 ? (hardPenalty, Penalty) : (0d, 0);
        }

        //protected override Solution FixRequired(Solution solution, Random random)
        //{
        //    var problem = solution.Problem;
        //    const int maxVariableLength = 10;

        //    if (Classes.Length == 0)
        //    {
        //        return solution;
        //    }

        //    var variables = problem.TimeVariablesSparse;
        //    var variable0 = variables[Classes[0]].Shuffle(random);

        //    var best = solution;
        //    bool Climb(Solution current, int index)
        //    {
        //        int variableLength;


        //        var variable = variables[index];
        //        var variableLength = Math.Min(variable.Length, maxVariableLength);
        //        if (variableLength > 0)
        //        {
        //            var @class = classes[index];
        //            for (var i = 0; i < variableLength; i++)
        //            {
        //                var time = variable[i];
        //                var candidate = current.WithTime(@class, time);
        //                if (candidate.NormalizedPenalty <= best.NormalizedPenalty)
        //                {
        //                    best = candidate;
        //                    return true;
        //                }

        //                if (index < max)
        //                {
        //                    if (Climb(candidate, index + 1))
        //                    {
        //                        return true;
        //                    }
        //                }
        //            }
        //        }
        //        else if (index < max)
        //        {
        //            if (Climb(current, index + 1))
        //            {
        //                return true;
        //            }
        //        }

        //        return false;
        //    }

        //    return best;
        //}
    }
}
