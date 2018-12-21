using System;
using System.Collections.Generic;
using System.Linq;
using Timetabling.Common.SolutionModel;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel.Constraints.Internal
{
    [Obsolete]
    public abstract class ConstraintBase : IConstraint
    {
        public abstract ConstraintType Type { get; }

        protected readonly int[] Classes;

        protected readonly HashSet<int> ClassesSet;

        protected ConstraintBase(int id, bool required, int penalty, int[] classes)
        {
            Id = id;
            Required = required;
            Penalty = penalty;
            Classes = classes.ToArray();
            ClassesSet = new HashSet<int>(classes);
        }

        public readonly int Id;

        public readonly bool Required;

        public int Difficulty { get; set; }

        int IConstraint.Id => Id;

        bool IConstraint.Required => Required;

        IEnumerable<int> IConstraint.Classes => Classes;

        public readonly int Penalty;

        public bool InvolvesClass(int @class)
        {
            return ClassesSet.Contains(@class);
        }

        public abstract (int hardPenalty, int softPenalty) Evaluate(ISolution s);

        public virtual Solution TryFix(Solution solution, Random random)
        {
            return Required
                ? FixRequired(solution, random)
                : Optimize(solution, random);
        }

        protected virtual Solution FixRequired(Solution solution, Random random)
        {
            return Optimize(solution, random);
        }

        protected virtual Solution Optimize(Solution solution, Random random)
        {
            const int maxClasses = 6;

            //Console.WriteLine($"Fixing constraint {(Required ? "R/" : "")}{GetType().Name}[{Classes.Length}]...");

            var shuffled = Utilities.Shuffle(Classes.Length, random);
            var classes = new int[Math.Min(Classes.Length, maxClasses)];
            for (var i = 0; i < classes.Length; i++)
            {
                classes[i] = Classes[shuffled[i]];
            }

            //var str = "";
            //foreach (var c in classes)
            //{
            //    str += $"C[{c}] R[{solution.GetRoomId(c)}] T[{solution.GetTime(c)}]; ";
            //}

            //Console.WriteLine(str);

            bool HardConstraint(Solution s)
            {
                return Evaluate(s).hardPenalty <= 0d;
            }

            bool SoftConstraint(Solution s)
            {
                return true;
            }

            var constraint = Required
                ? new Func<Solution, bool>(HardConstraint)
                : SoftConstraint;

            var type = Type;
            Solution result;
            if (type == ConstraintType.Time)
            {
                result = solution.RandomizedTimeClimb(classes, random, constraint)/*.Log("Done")*/;
            }
            else if (type == ConstraintType.Room)
            {
                result = solution.RandomizedRoomClimb(classes, random, constraint)/*.Log("Done")*/;
            }
            else
            {
                result = solution.RandomizedTimeClimb(classes, random, constraint)/*.Log("Done")*/;
            }

            var str = "";
            foreach (var c in classes)
            {
                str += $"C[{c}] R[{result.GetRoomId(c)}] T[{result.GetTime(c)}]; ";
            }

            //Console.WriteLine(str);
            return result;
        }
    }
}
