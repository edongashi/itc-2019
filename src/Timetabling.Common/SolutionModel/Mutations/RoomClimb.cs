using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class RoomClimb : IMutation
    {
        public readonly int Max;

        public RoomClimb(int max)
        {
            Max = max;
        }

        public Solution Mutate(Solution solution, Random random)
        {
            return solution.RandomizedRoomClimb(
                solution.Problem.PluckRoomClasses(1 + random.Next(Max), random),
                random,
                _ => true);
        }
    }
}
