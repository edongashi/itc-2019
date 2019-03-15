using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Timetabling.Common.ProblemModel;
using Timetabling.Common.SolutionModel;
using Timetabling.Common.SolutionModel.Mutations;

namespace Timetabling.Common
{
    public class SimulatedAnnealingSolver
    {
        private static readonly object RandomLock = new object();

        public double MaxTemperature = 0.25d;

        public double MaxFeasibleTemperature = 1E-8d;

        public double TemperatureChange = 0.999995d;

        public int PenalizeTimeout = 200_000;

        public Solution Solve(Problem problem, Solution initialSolution, CancellationToken cancellation)
        {
            const int studentRandomizationNoise = 20;
            if (initialSolution == null)
            {
                initialSolution = problem.InitialSolution;
            }

            var feasibleMutations = GetFeasibleMutations(problem);
            if (feasibleMutations.Count == 0)
            {
                return initialSolution;
            }

            var unfeasibleMutations = GetUnfeasibleMutations(problem);
            if (unfeasibleMutations.Count == 0)
            {
                return initialSolution;
            }

            var penalizedUnfeasibleMutations = GetPenalizedUnfeasibleMutations(problem);
            if (penalizedUnfeasibleMutations.Count == 0)
            {
                return initialSolution;
            }


            Random random;
            lock (RandomLock)
            {
                random = new Random();
            }

            var s = initialSolution;
            Console.WriteLine("=== Empty Solution ===");
            s.PrintStats();

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

            Console.WriteLine($"[{s.Penalty}] Done");

            Console.WriteLine("Assigning enrollments...");
            foreach (var variable in problem.StudentVariables.OrderBy(_ => random.Next()))
            {
                for (var j = 0; j < studentRandomizationNoise; j++)
                {
                    s = s.WithEnrollment(variable.Student,
                        variable.LooseValues[random.Next(variable.LooseValues.Length)]);
                }
            }

            Console.WriteLine($"[{s.Penalty}] Done");

            Console.WriteLine($"Initial solution penalty: {s.Penalty}");

            var best = s;
            if (initialSolution.Penalty < best.Penalty)
            {
                s = initialSolution;
                best = initialSolution;
            }

            var t = MaxTemperature;
            var i = 0;

            var (timePenalties, roomPenalties) = problem.CreatePenaltyMap();
            var penalizations = 0;
            var timeout = 0;

            void Penalize(Solution solution)
            {
                penalizations++;

                var states = solution.ClassStates;
                var extraPenalty = 0d;
                void PenalizeTime(int @class)
                {
                    var cls = problem.Classes[@class];
                    var state = states[@class];
                    if (cls.TimeConstraints.Length > 1)
                    {
                        var val = timePenalties[@class].Values[state.Time];
                        timePenalties[@class].Values[state.Time] = (val <= 0d ? 0.005d : 1.1d * val + 0.01d) + extraPenalty;
                    }
                }

                void PenalizeRoom(int @class)
                {
                    var cls = problem.Classes[@class];
                    var state = states[@class];

                    if (cls.RoomConstraints.Length > 1)
                    {
                        var val = roomPenalties[@class].Values[state.Room];
                        roomPenalties[@class].Values[state.Room] = (val <= 0d ? 0.005d : 1.1d * val + 0.01d) + extraPenalty;
                    }
                }

                var constraintStates = solution
                    .ConstraintStates
                    .Select((state, index) => (state, index))
                    .Where(pair => pair.state.HardPenalty > 0)
                    .ToList();
                var conflictingClasses = solution.ConflictingClasses();
                var unavailableClasses = solution.RoomUnavailableClasses();
                if (constraintStates.Count > 0 || conflictingClasses.Count > 0 || unavailableClasses.Count > 0)
                {
                    extraPenalty = 0.25d;
                    foreach (var (_, index) in constraintStates)
                    {
                        var constraint = problem.Constraints[index];
                        var classes = constraint.EvaluateConflictingClasses(solution);
                        switch (constraint.Type)
                        {
                            case ConstraintType.Common:
                                classes.ForEach(c =>
                                {
                                    PenalizeTime(c);
                                    PenalizeRoom(c);
                                });
                                break;
                            case ConstraintType.Time:
                                classes.ForEach(PenalizeTime);
                                break;
                            case ConstraintType.Room:
                                classes.ForEach(PenalizeRoom);
                                break;
                        }
                    }

                    foreach (var cl in conflictingClasses)
                    {
                        PenalizeTime(cl);
                        PenalizeRoom(cl);
                    }

                    foreach (var cl in unavailableClasses)
                    {
                        PenalizeTime(cl);
                        PenalizeRoom(cl);
                    }

                    return;
                }

                var length = states.Length;
                //var timePenaltiesCandidates = new double[length];
                //var roomPenaltiesCandidates = new double[length];
                for (var j = 0; j < length; j++)
                {
                    PenalizeTime(j);
                    PenalizeRoom(j);
                    //timePenaltiesCandidates[j] = timePenalties[j].Values[state.Time];
                    //roomPenaltiesCandidates[j] = double.PositiveInfinity;
                    //if (state.Room >= 0)
                    //{
                    //    roomPenaltiesCandidates[j] = roomPenalties[j].Values[state.Room];
                    //}
                }

                //var minTime = timePenaltiesCandidates.Min();
                //var minRoom = roomPenaltiesCandidates.Min();

                //for (var j = 0; j < length; j++)
                //{
                //    var state = states[j];

                //    var timeVal = timePenalties[j].Values[state.Time];
                //    if (timeVal <= minTime)
                //    {
                //        timePenalties[j].Values[state.Time] = timeVal + 0.001d;
                //    }

                //    if (!double.IsPositiveInfinity(minRoom) && state.Room >= 0)
                //    {
                //        var roomVal = roomPenalties[j].Values[state.Room];
                //        if (roomVal <= minRoom)
                //        {
                //            roomPenalties[j].Values[state.Room] = roomVal + 0.001d;
                //        }
                //    }
                //}
            }

            double Adjustment(Solution solution)
            {
                if (penalizations == 0)
                {
                    return 0d;
                }

                var result = 0d;
                var states = solution.ClassStates;
                var length = states.Length;
                for (var j = 0; j < length; j++)
                {
                    var state = states[j];
                    result += timePenalties[j].Values[state.Time];
                    if (state.Room >= 0)
                    {
                        result += roomPenalties[j].Values[state.Room];
                    }
                }

                return result;
            }

            var sAdjustment = 0d;

            void ClearPenalties()
            {
                for (var j = 0; j < timePenalties.Length; j++)
                {
                    var p = timePenalties[j];
                    for (var k = 0; k < p.Values.Length; k++)
                    {
                        p.Values[k] = 0;
                    }
                }

                for (var j = 0; j < timePenalties.Length; j++)
                {
                    var p = roomPenalties[j];
                    for (var k = 0; k < p.Values.Length; k++)
                    {
                        p.Values[k] = 0;
                    }
                }

                sAdjustment = 0d;
                penalizations = 0;
            }

            (Solution, double) Mutate(Solution solution)
            {
                IMutation mutation;
                if (solution.HardPenalty == 0)
                {
                    mutation = feasibleMutations[random.Next(feasibleMutations.Count)];
                }
                else if (penalizations > 0)
                {
                    mutation = penalizedUnfeasibleMutations[random.Next(penalizedUnfeasibleMutations.Count)];
                }
                else
                {
                    mutation = unfeasibleMutations[random.Next(unfeasibleMutations.Count)];
                }

                return mutation.Mutate(solution, random, penalizations, timePenalties, roomPenalties);
            }

            var focus = new HashSet<int>();
            foreach (var constraint in problem.HardConstraints())
            {
                if (constraint.Classes.Count() > 20)
                {
                    focus.Add(constraint.Id);
                }
            }

            while (true)
            {
                if (cancellation.IsCancellationRequested)
                {
                    Console.WriteLine("Cancelling solver...");
                    break;
                }

                var (sc, penaltyDelta) = Mutate(s);
                var cAdjustment = sAdjustment + penaltyDelta;

                if (sc.HardPenalty < best.HardPenalty || sc.HardPenalty == best.HardPenalty && sc.SoftPenalty < best.SoftPenalty)
                {
                    if (sc.HardPenalty < best.HardPenalty)
                    {
                        ClearPenalties();
                        sAdjustment = 0d;
                        cAdjustment = 0d;
                        timeout = 0;
                    }

                    best = sc;
                    if (best.HardPenalty <= 0 || penalizations > 0)
                    {
                        timeout = 0;
                    }
                }
                else
                {
                    timeout++;
                    if (timeout >= PenalizeTimeout)
                    {
                        timeout = 0;
                        Penalize(s);
                        sAdjustment = Adjustment(s);
                        cAdjustment = Adjustment(sc);
                        t = 0.25d;// + (penalizations - 1) * 0.2d;
                    }
                }

                t = t * TemperatureChange;
                if (s.HardPenalty == 0 && t > MaxFeasibleTemperature)
                {
                    t = MaxFeasibleTemperature;
                }

                var sPenalty = s.SearchPenalty + sAdjustment;
                var cPenalty = sc.SearchPenalty + cAdjustment;

                if (s.HardPenalty == 0 && sc.HardPenalty == 0)
                {
                    var sSoft = s.SoftPenalty + sAdjustment;
                    var cSoft = sc.SoftPenalty + cAdjustment;
                    sPenalty = sSoft / (1d + sSoft);
                    cPenalty = cSoft / (1d + cSoft);
                }

                var stotal = 0;
                var sctotal = 0;
                if (s.HardPenalty > 0 && focus.Count > 0)
                {
                    var sConstraintStates = s.ConstraintStates;
                    var scConstraintStates = sc.ConstraintStates;
                    foreach (var constraint in focus)
                    {
                        stotal += sConstraintStates[constraint].HardPenalty;
                        sctotal += scConstraintStates[constraint].HardPenalty;
                    }

                    if (sctotal < stotal)
                    {
                        s = sc;
                        sAdjustment = cAdjustment;
                    }
                }

                if (sctotal < stotal)
                {
                    s = sc;
                    sAdjustment = cAdjustment;
                    if (cPenalty < sPenalty)
                    {
                        timeout = 0;
                    }
                }
                else if (cPenalty <= sPenalty)
                {
                    if (sctotal == stotal || random.NextDouble() >= 0.3d)
                    {
                        s = sc;
                        sAdjustment = cAdjustment;
                        if (cPenalty < sPenalty)
                        {
                            timeout = 0;
                        }
                    }
                }
                else if (Math.Exp((sPenalty - cPenalty) / t) > random.NextDouble())
                {
                    s = sc;
                    sAdjustment = cAdjustment;
                }

                if (++i % 1_000 == 0)
                {
                    Console.WriteLine($"Cycle[{i}] Quality[{s.Penalty} + {sAdjustment}] Temp[{t}] Timeout[{timeout}]");
                    if (i % 10_000 == 0)
                    {
                        s.PrintStats();
                        if (i % 1_000_000 == 0)
                        {
                            try
                            {
                                Console.WriteLine("Creating backup...");
                                best.Serialize().Save("_backup_" + (problem.Name ?? "problem") + ".xml");
                            }
                            catch
                            {
                                Console.WriteLine("Error creating backup.");
                            }
                        }
                    }
                }
            }

            return best;
        }

        private static List<IMutation> GetUnfeasibleMutations(Problem problem)
        {
            var mutations = new List<IMutation>();
            if (problem.AllClassVariables.Length > 0)
            {
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(6));
            }

            if (problem.StudentVariables.Length > 0)
            {
                mutations.Add(new StudentMutation());
            }

            if (problem.Constraints.Length > 0)
            {

            }

            return mutations;
        }

        private static List<IMutation> GetPenalizedUnfeasibleMutations(Problem problem)
        {
            var mutations = new List<IMutation>();
            if (problem.AllClassVariables.Length > 0)
            {
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(6));
            }

            if (problem.StudentVariables.Length > 0)
            {
                mutations.Add(new StudentMutation());
            }

            if (problem.Constraints.Length > 0)
            {

            }

            return mutations;
        }

        private static List<IMutation> GetFeasibleMutations(Problem problem)
        {
            var mutations = new List<IMutation>();
            if (problem.RoomVariables.Length > 0)
            {

            }

            if (problem.TimeVariables.Length > 0)
            {

            }

            if (problem.AllClassVariables.Length > 0)
            {
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(1));
                mutations.Add(new VariableMutation(6));
            }

            if (problem.StudentVariables.Length > 0)
            {
                mutations.Add(new StudentMutation());
                mutations.Add(new StudentMutation());
                mutations.Add(new StudentMutation());
            }

            if (problem.Constraints.Length > 0)
            {

            }

            return mutations;
        }
    }
}
