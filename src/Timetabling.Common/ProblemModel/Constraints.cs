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

        bool InvolvesClass(int @class);

        (double hardPenalty, int softPenalty) Evaluate(Problem p, Solution s);

        (double hardPenalty, int softPenalty) Evaluate(Problem p, Solution s, ClassOverride @override);
    }

    public static class ConstraintExtensions
    {
        public static (double hardPenalty, int softPenalty) Evaluate(
            this IEnumerable<IConstraint> @this,
            Problem p,
            Solution s,
            ClassOverride @override)
        {
            var hardPenalty = 0d;
            var softPenalty = 0;
            foreach (var constraint in @this)
            {
                var (hp, sp) = constraint.Evaluate(p, s, @override);
                hardPenalty += hp;
                softPenalty += sp;
            }

            return (hardPenalty, softPenalty);
        }

        public static (double hardPenalty, int softPenalty) Evaluate(
            this IEnumerable<IConstraint> @this,
            Problem p,
            Solution s)
        {
            var hardPenalty = 0d;
            var softPenalty = 0;
            foreach (var constraint in @this)
            {
                var (hp, sp) = constraint.Evaluate(p, s);
                hardPenalty += hp;
                softPenalty += sp;
            }

            return (hardPenalty, softPenalty);
        }
    }
}
