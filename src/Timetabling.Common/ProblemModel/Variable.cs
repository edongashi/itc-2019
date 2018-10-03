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
        public CourseVariable(int student, int[][] values)
        {
            Student = student;
            Values = values;
            Configurations = values.Length;
        }

        public readonly int Student;

        public readonly int Configurations;

        // Outer array represents configurations,
        // while inner array represents classes
        public readonly int[][] Values;
    }

    //public struct CourseConfiguration
    //{
    //    public CourseConfiguration(int courseIndex, int[] variables)
    //    {
    //        CourseIndex = courseIndex;
    //        Variables = variables;
    //    }

    //    public readonly int CourseIndex;

    //    public readonly int[] Variables;
    //}
}