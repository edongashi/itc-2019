using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class RoomMutation : IMutation
    {
        public readonly int Max;

        public RoomMutation(int max)
        {
            Max = max;
        }

        public (Solution solution, double temperature) Mutate(Solution solution, Random random)
        {
            var vars = solution.Problem.RoomVariables;
            var count = 1 + random.Next(Max);
            var result = solution;
            for (var i = 0; i < count; i++)
            {
                var var = vars[random.Next(vars.Length)];
                result = result.WithRoom(var.Class, random.Next(var.MaxValue));
            }

            return (result, 0d);
        }
    }
}