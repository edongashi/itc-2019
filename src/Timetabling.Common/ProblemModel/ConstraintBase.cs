using System.Collections.Generic;
using System.Linq;
using Timetabling.Common.SolutionModel;

namespace Timetabling.Common.ProblemModel
{
    public abstract class ConstraintBase : IConstraint
    {
        public abstract ConstraintType Type { get; }

        protected readonly int[] Classes;

        protected readonly HashSet<int> ClassesSet;

        protected ConstraintBase(bool required, int penalty, int[] classes)
        {
            Required = required;
            Penalty = penalty;
            Classes = classes.ToArray();
            ClassesSet = new HashSet<int>(classes);
        }

        public readonly bool Required;

        public readonly int Penalty;

        public bool InvolvesClass(int @class)
        {
            return ClassesSet.Contains(@class);
        }

        public abstract (double hardPenalty, int softPenalty) Evaluate(ISolution s);
    }
}
