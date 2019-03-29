using System;
using Timetabling.Common.ProblemModel;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class ConstraintMutation : IMutation
    {
        private readonly VariableMutation fallback;

        public ConstraintMutation()
        {
            fallback = new VariableMutation(1);
        }

        public (Solution solution, double penaltyDelta) Mutate(
            Solution solution,
            Random random,
            int penalizations,
            VariablePenalty[] timePenalties,
            VariablePenalty[] roomPenalties)
        {
            var constraints = solution.Problem.Constraints;
            var timeVariables = solution.Problem.TimeVariablesSparse;
            var roomVariables = solution.Problem.RoomVariablesSparse;
            var states = solution.ConstraintStates;
            var seed = random.Next(states.Length);
            var delta = 0d;
            var found = false;
            for (var j = 0; j < states.Length; j++)
            {
                var id = (seed + j) % states.Length;
                if (states[id].HardPenalty <= 0)
                {
                    continue;
                }

                var constraint = constraints[id];
                foreach (var @class in constraint.Classes)
                {
                    var tval = random.Next(timeVariables[@class].MaxValue);
                    solution = solution.WithTime(@class, tval);
                    var roomVariable = roomVariables[@class];
                    if (penalizations > 0)
                    {
                        var penalties = timePenalties[@class].Values;
                        delta += penalties[tval] - penalties[solution.ClassStates[@class].Time];
                    }

                    if (roomVariable.Type == VariableType.Room)
                    {
                        var rval = random.Next(roomVariable.MaxValue);
                        solution = solution.WithRoom(@class, rval);
                        if (penalizations > 0)
                        {
                            var penalties = roomPenalties[@class].Values;
                            delta += penalties[rval] - penalties[solution.ClassStates[@class].Room];
                        }
                    }
                }

                found = true;
                break;
            }

            if (!found)
            {
                return fallback.Mutate(solution, random, penalizations, timePenalties, roomPenalties);
            }

            return (solution, delta);
        }
    }
}
