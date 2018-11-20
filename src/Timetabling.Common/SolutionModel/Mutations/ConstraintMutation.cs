using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices.ComTypes;
using Timetabling.Common.ProblemModel;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class ConstraintMutation : IMutation
    {
        public readonly int Max;

        public ConstraintMutation(int max)
        {
            Max = max;
        }

        public (Solution solution, bool forceAccept) Mutate(Solution solution, Random random)
        {
            var constraints = solution.Problem.Constraints;
            var count = 1 + random.Next(Max);
            var timeVariables = solution.Problem.TimeVariablesSparse;
            var roomVariables = solution.Problem.RoomVariablesSparse;
            var states = solution.ConstraintStates;
            for (var i = 0; i < count; i++)
            {
                var seed = random.Next(states.Length);
                var found = false;
                for (var j = 0; j < states.Length; j++)
                {
                    var id = (seed + j) % states.Length;
                    if (states[id].HardPenalty <= 0d)
                    {
                        continue;
                    }

                    var constraint = constraints[id];
                    var variables = timeVariables;
                    if (constraint.Type == ConstraintType.Room)
                    {
                        variables = roomVariables;
                    }

                    foreach (var @class in constraint.Classes)
                    {
                        var variable = variables[@class];
                        solution = solution.WithVariable(@class, random.Next(variable.MaxValue), variable.Type);
                    }

                    found = true;
                    break;
                }

                if (!found)
                {
                    var constraint = constraints[seed];
                    var variables = timeVariables;
                    if (constraint.Type == ConstraintType.Room)
                    {
                        variables = roomVariables;
                    }

                    foreach (var @class in constraint.Classes)
                    {
                        var variable = variables[@class];
                        solution = solution.WithVariable(@class, random.Next(variable.MaxValue), variable.Type);
                    }
                }
            }

            return (solution, true);
        }
    }
}
