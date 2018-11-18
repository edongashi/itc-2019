using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class StudentMutation : IMutation
    {
        public readonly int Max;

        public StudentMutation(int max)
        {
            Max = max;
        }

        public Solution Mutate(Solution solution, Random random)
        {
            var vars = solution.Problem.StudentVariables;
            var count = 1 + random.Next(Max);
            var result = solution;
            for (var i = 0; i < count; i++)
            {
                var var = vars[random.Next(vars.Length)];
                var classes = var.LooseValues;
                result = result.WithEnrollment(var.Student, classes[random.Next(classes.Length)]);
            }

            return result;
        }
    }
}
