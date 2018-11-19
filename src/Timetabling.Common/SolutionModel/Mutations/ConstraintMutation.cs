using System;
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

        public Solution Mutate(Solution solution, Random random)
        {
            var constraints = solution.Problem.Constraints;
            var count = 1 + random.Next(Max);
            var result = solution;
            var timeVariables = solution.Problem.TimeVariablesSparse;
            var roomVariables = solution.Problem.RoomVariablesSparse;
            for (var i = 0; i < count; i++)
            {
                var constraint = constraints[random.Next(constraints.Length)];
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

            return result;
        }
    }
}
