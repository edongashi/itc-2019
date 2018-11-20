using System;
using System.Collections.Generic;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel
{
    public enum ConstraintType
    {
        Common,
        Time,
        Room
    }

    public interface IConstraint
    {
        int Id { get; }

        ConstraintType Type { get; }

        bool Required { get; }

        IEnumerable<int> Classes { get; }

        bool InvolvesClass(int @class);

        (int hardPenalty, int softPenalty) Evaluate(ISolution s);

        Solution TryFix(Solution solution, Random random);
    }
}
