using System.IO;
using Timetabling.Common.ProblemModel;

namespace Timetabling.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            using (var stream = File.OpenRead(args[0]))
            {
                var problem = ProblemParser.FromXml(stream);
                var solution = problem.InitialSolution;
            }
        }
    }
}
