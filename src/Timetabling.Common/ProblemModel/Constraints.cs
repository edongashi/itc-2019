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
        ConstraintType Type { get; }

        bool Required { get; }

        bool InvolvesClass(int @class);

        (double hardPenalty, int softPenalty) Evaluate(ISolution s);

        Solution TryFix(Solution solution, Random random);
    }

    public static class ConstraintExtensions
    {
        public static (double hardPenalty, int softPenalty) Evaluate(
            this IEnumerable<IConstraint> @this,
            ISolution s)
        {
            var hardPenalty = 0d;
            var softPenalty = 0;
            foreach (var constraint in @this)
            {
                var (hp, sp) = constraint.Evaluate(s);
                hardPenalty += hp;
                softPenalty += sp;
            }

            return (hardPenalty, softPenalty);
        }
    }
}
