using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class TimeMutation : IMutation
    {
        public readonly int Max;

        public TimeMutation(int max)
        {
            Max = max;
        }

        public (Solution solution, double temperature) Mutate(Solution solution, Random random)
        {
            var vars = solution.Problem.TimeVariables;
            var count = 1 + random.Next(Max);
            var result = solution;
            for (var i = 0; i < count; i++)
            {
                var var = vars[random.Next(vars.Length)];
                result = result.WithTime(var.Class, random.Next(var.MaxValue));
            }

            return (result, 0d);
        }
    }
}