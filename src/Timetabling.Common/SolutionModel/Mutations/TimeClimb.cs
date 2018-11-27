using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class TimeClimb : IMutation
    {
        public readonly int Max;

        public TimeClimb(int max)
        {
            Max = max;
        }

        public (Solution solution, double temperature) Mutate(Solution solution, Random random)
        {
            return (solution.RandomizedTimeClimb(
                solution.Problem.PluckTimeClasses(1 + random.Next(Max), random),
                random,
                _ => true), 0d);
        }
    }
}
