using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class MinGap : ConstraintBase
    {
        public MinGap(int g, bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
            G = g;
        }

        public readonly int G;

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var hardPenalty = 0;
            for (var i = 0; i < Classes.Length - 1; i++)
            {
                var ci = s.GetTime(Classes[i]);
                for (var j = i + 1; j < Classes.Length; j++)
                {
                    var cj = s.GetTime(Classes[i]);
                    if ((ci.Days & cj.Days) == 0u
                        || (ci.Weeks & cj.Weeks) == 0u
                        || ci.End + G <= cj.Start
                        || cj.End + G <= ci.Start)
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
    }
}
