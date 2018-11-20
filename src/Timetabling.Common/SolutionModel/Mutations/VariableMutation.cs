using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class VariableMutation : IMutation
    {
        public readonly int Max;

        public VariableMutation(int max)
        {
            Max = max;
        }

        public (Solution solution, bool forceAccept) Mutate(Solution solution, Random random)
        {
            var vars = solution.Problem.AllClassVariables;
            var count = 1 + random.Next(Max);
            var result = solution;
            for (var i = 0; i < count; i++)
            {
                var var = vars[random.Next(vars.Length)];
                result = result.WithVariable(var.Class, random.Next(var.MaxValue), var.Type);
            }

            return (result, false);
        }
    }
}
