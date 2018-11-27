using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class ConstraintClimb : IMutation
    {
        public readonly int Max;

        public ConstraintClimb(int max)
        {
            Max = max;
        }

        public (Solution solution, double temperature) Mutate(Solution solution, Random random)
        {
            var constraints = solution.Problem.Constraints;
            var count = 1 + random.Next(Max);
            var result = solution;
            for (var i = 0; i < count; i++)
            {
                var constraint = constraints[random.Next(constraints.Length)];
                result = constraint.TryFix(result, random);
            }

            return (result, 0d);
        }
    }
}
