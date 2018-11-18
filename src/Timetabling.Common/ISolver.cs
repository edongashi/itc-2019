using System.Threading;
using Timetabling.Common.ProblemModel;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common
{
    public interface ISolver
    {
        Solution Solve(Problem problem, CancellationToken cancellation);
    }
}
