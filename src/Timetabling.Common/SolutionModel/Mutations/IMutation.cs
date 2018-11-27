using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public interface IMutation
    {
        (Solution solution, double temperature) Mutate(Solution solution, Random random);
    }
}
