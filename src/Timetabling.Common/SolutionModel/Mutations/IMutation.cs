using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public interface IMutation
    {
        Solution Mutate(Solution solution, Random random);
    }
}
