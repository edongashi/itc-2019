using System;
using Timetabling.Common.ProblemModel;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public interface IMutation
    {
        (Solution solution, double penaltyDelta) Mutate(
            Solution solution,
            Random random,
            int penalizations,
            VariablePenalty[] timePenalties,
            VariablePenalty[] roomPenalties);
    }
}
