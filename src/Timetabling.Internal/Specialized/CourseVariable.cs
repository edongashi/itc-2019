using System.Linq;

namespace Timetabling.Internal.Specialized
{
    public struct CourseVariable
    {
        internal CourseVariable(int student, int[][][] values)
        {
            Student = student;
            Values = values;
            LooseValues = values.SelectMany(c => c.SelectMany(s => s)).ToArray();
        }

        public readonly int Student;

        public readonly int[] LooseValues;

        // Configurations -> Child Subparts -> Classes
        internal readonly int[][][] Values;
    }
}