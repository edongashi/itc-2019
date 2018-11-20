using System;

namespace Timetabling.Common.SolutionModel.Mutations
{
    public interface IMutation
    {
        (Solution solution, bool forceAccept) Mutate(Solution solution, Random random);
    }
}
