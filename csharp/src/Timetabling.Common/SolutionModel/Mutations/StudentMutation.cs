using System;
using Timetabling.Common.ProblemModel;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public class StudentMutation : IMutation
    {
        public (Solution solution, double penaltyDelta) Mutate(
            Solution solution,
            Random random,
            int penalizations,
            VariablePenalty[] timePenalties,
            VariablePenalty[] roomPenalties)
        {
            var vars = solution.Problem.StudentVariables;
            var var = vars[random.Next(vars.Length)];
            var classes = var.LooseValues;
            return (solution.WithEnrollment(var.Student, classes[random.Next(classes.Length)]), 0d);
        }
    }
}
