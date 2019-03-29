using System;
using Timetabling.Common.ProblemModel;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class VariableMutation : IMutation
    {
        public VariableMutation(int max)
        {
            Max = max;
        }

        public readonly int Max;

        public (Solution solution, double penaltyDelta) Mutate(
            Solution solution,
            Random random,
            int penalizations,
            VariablePenalty[] timePenalties,
            VariablePenalty[] roomPenalties)
        {
            var vars = solution.Problem.AllClassVariables;
            var count = 1 + random.Next(Max);
            var delta = 0d;
            for (var i = 0; i < count; i++)
            {
                var var = vars[random.Next(vars.Length)];
                var val = random.Next(var.MaxValue);
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (var.Type == VariableType.Time)
                {
                    if (penalizations != 0)
                    {
                        var penalties = timePenalties[var.Class].Values;
                        delta += penalties[val] - penalties[solution.ClassStates[var.Class].Time];
                    }

                    solution = solution.WithTime(var.Class, val);
                }
                else
                {
                    if (penalizations != 0)
                    {
                        var penalties = roomPenalties[var.Class].Values;
                        delta += penalties[val] - penalties[solution.ClassStates[var.Class].Room];
                    }

                    solution = solution.WithRoom(var.Class, val);
                }
            }

            return (solution, delta);
        }
    }
}
