namespace Timetabling.Common.ProblemModel
{
    public struct Variable
    {
        public Variable(int @class, int maxValue)
        {
            Class = @class;
            MaxValue = maxValue;
        }

        public readonly int Class;

        public readonly int MaxValue;
    }

    public struct CourseVariable
    {
        public CourseVariable(int student, int[][][] values)
        {
            Student = student;
            Values = values;
        }

        public readonly int Student;

        // Configurations -> Child Subparts -> Classes
        public readonly int[][][] Values;
    }
}