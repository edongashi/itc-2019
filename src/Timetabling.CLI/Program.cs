using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Timetabling.Common;
using Timetabling.Common.ProblemModel;

namespace Timetabling.CLI
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            Problem problem;
            using (var stream = File.OpenRead(args[0]))
            {
                problem = ProblemParser.FromXml(stream);
            }

            var solver = new Solver();
            var cancellation = new CancellationTokenSource();

            // ReSharper disable once MethodSupportsCancellation
            Task.Run(() =>
            {
                while (true)
                {
                    var input = Console.ReadKey(true);
                    if (input.Key == ConsoleKey.C)
                    {
                        cancellation.Cancel();
                        break;
                    }
                }
            });

            var solution = solver.Solve(problem, cancellation.Token);
            Console.WriteLine("=== Final Solution ===");
            solution.PrintStats();
            solution
                .Log("Serializing solution...")
                .Serialize()
                .Log("Saving solution...")
                .Save("solution.xml");
            Console.WriteLine("Solution saved");
            Console.Write("Press any key to continue . . . ");
            Console.ReadKey(true);

            //var random = new Random();
            //var solution = problem.InitialSolution;
            //var best = solution;
            //Console.WriteLine(solution.NormalizedPenalty);
            //var stopwatch = Stopwatch.StartNew();
            //var accept = 1_000_000;
            //while (stopwatch.ElapsedMilliseconds <= 600_000L)
            //{
            //    var room = random.Next(2) == 0;
            //    Solution next;
            //    if (room)
            //    {
            //        var variable = problem.RoomVariables[random.Next(problem.RoomVariables.Length)];
            //        next = solution.WithRoom(variable.Class, random.Next(variable.MaxValue));
            //    }
            //    else
            //    {
            //        var variable = problem.TimeVariables[random.Next(problem.TimeVariables.Length)];
            //        next = solution.WithTime(variable.Class, random.Next(variable.MaxValue));
            //    }

            //    if (next.NormalizedPenalty < best.NormalizedPenalty)
            //    {
            //        best = next;
            //        Console.WriteLine(solution.NormalizedPenalty);
            //    }

            //    if (next.NormalizedPenalty < solution.NormalizedPenalty)
            //    {
            //        solution = next;
            //    }
            //    else if (random.Next(1_000_000) <= accept)
            //    {
            //        solution = next;
            //        accept -= 20;
            //    }
            //}

            //best.Serialize().Save("solution.xml");
        }
    }
}
