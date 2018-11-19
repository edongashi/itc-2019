using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Timetabling.Common.ProblemModel;
using Timetabling.Common.SolutionModel;
using Timetabling.Common.SolutionModel.Mutations;

namespace Timetabling.Common
{
    public class Solver
    {
        private static readonly object RandomLock = new object();

        public double MaxTemperature = 0.5d;

        public double TemperatureChange = 0.999999d;

        public Solution Solve(Problem problem, CancellationToken cancellation)
        {
            const int studentRandomizationNoise = 20;

            var mutations = GetMutations(problem);
            if (mutations.Count == 0)
            {
                return problem.InitialSolution;
            }

            Random random;
            lock (RandomLock)
            {
                random = new Random();
            }

            Solution Mutate(Solution solution)
            {
                var mutation = mutations[random.Next(mutations.Count)];
                return mutation.Mutate(solution, random);
            }

            var s = problem.InitialSolution;
            Console.WriteLine($"[{s.Penalty}] Empty solution...");

            {
                var solution = s;
                Console.WriteLine("=== Empty Solution ===");
                var (hrd, sft) = solution.CalculatePenalty();
                Console.WriteLine($"Hard penalty: {hrd}, Soft: {sft}, Normalized: {solution.Penalty}");
                Console.WriteLine($"Hard penalty: {solution.HardPenalty}, Soft: {solution.SoftPenalty}");
                Console.WriteLine($"Time penalty: {solution.TimePenalty()} Room penalty: {solution.RoomPenalty()} Dist penalty: {solution.DistributionPenalty()}");
                Console.WriteLine($"Failures: Hard: {solution.FailedHardConstraints()}, Soft: {solution.FailedSoftConstraints()}");
            }


            Console.WriteLine("Assigning class variables...");
            foreach (var variable in problem.AllClassVariables.OrderBy(_ => random.Next()))
            {
                if (variable.Type == VariableType.Time)
                {
                    s = s.WithTime(variable.Class, random.Next(variable.MaxValue));
                }
                else if (variable.Type == VariableType.Room)
                {
                    s = s.WithRoom(variable.Class, random.Next(variable.MaxValue));
                }
            }

            Console.WriteLine($"[{s.Penalty}] Done...");

            Console.WriteLine("Assigning enrollments...");
            foreach (var variable in problem.StudentVariables.OrderBy(_ => random.Next()))
            {
                for (var j = 0; j < studentRandomizationNoise; j++)
                {
                    s = s.WithEnrollment(variable.Student,
                        variable.LooseValues[random.Next(variable.LooseValues.Length)]);
                }
            }

            Console.WriteLine($"[{s.Penalty}] Done...");

            //var counter = 0;
            //Console.WriteLine("Fixing constraints...");
            //foreach (var constraint in problem.Constraints.OrderBy(_ => random.Next()))
            //{
            //    var sc = constraint.TryFix(s, random);
            //    Console.WriteLine($"[{s.Penalty}] Constraint {++counter}/{problem.Constraints.Length}");
            //    if (sc.Penalty < s.Penalty)
            //    {
            //        s = sc;
            //    }
            //}

            //Console.WriteLine($"[{s.Penalty}] Done...");

            //counter = 0;
            //Console.WriteLine("Fixing required constraints...");
            //var requiredConstraints = problem.Constraints.Where(c => c.Required).ToArray();
            //foreach (var constraint in requiredConstraints.OrderBy(_ => random.Next()))
            //{
            //    var sc = constraint.TryFix(s, random);
            //    Console.WriteLine($"[{s.Penalty}] Required constraint {++counter}/{requiredConstraints.Length}");
            //    if (sc.Penalty < s.Penalty)
            //    {
            //        s = sc;
            //    }
            //}

            //Console.WriteLine($"[{s.Penalty}] Done...");

            Console.WriteLine($"Initial solution penalty: {s.Penalty}");

            var best = s;
            var t = MaxTemperature;
            var i = 0;
            while (true)
            {
                if (cancellation.IsCancellationRequested)
                {
                    Console.WriteLine("Cancelling solver...");
                    break;
                }

                t = t * TemperatureChange;

                var sc = Mutate(s);
                if (sc.Penalty <= best.Penalty)
                {
                    best = sc;
                    //Console.WriteLine($"New best [{sc.Penalty}]");
                }

                if (sc.Penalty <= s.Penalty)
                {
                    s = sc;
                }
                else if (Math.Exp((s.Penalty - sc.Penalty) / t) > random.NextDouble())
                {
                    s = sc;
                    //Console.WriteLine($"Accepting worse solution [{sc.Penalty}]");
                }

                if (++i % 1000 == 0)
                {
                    Console.WriteLine($"Cycle[{i}] Quality[{s.Penalty}] Temp[{t}]");
                }
            }

            return best;
        }

        private static List<IMutation> GetMutations(Problem problem)
        {
            var mutations = new List<IMutation>();
            if (problem.RoomVariables.Length > 0)
            {
                //mutations.Add(new RoomClimb(2));
                //mutations.Add(new RoomClimb(1));
                //mutations.Add(new RoomClimb(5));
                //mutations.Add(new RoomMutation(1));
            }

            if (problem.TimeVariables.Length > 0)
            {
                //mutations.Add(new TimeClimb(2));
                //mutations.Add(new TimeClimb(1));
                //mutations.Add(new TimeClimb(5));
                //mutations.Add(new TimeMutation(1));
            }

            if (problem.AllClassVariables.Length > 0)
            {
                mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(10));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
                //mutations.Add(new VariableMutation(1));
            }

            if (problem.StudentVariables.Length > 0)
            {
                mutations.Add(new StudentMutation(1));
            }

            if (problem.Constraints.Length > 0)
            {
                //mutations.Add(new ConstraintMutation(1));
            }

            return mutations;
        }
    }
}
