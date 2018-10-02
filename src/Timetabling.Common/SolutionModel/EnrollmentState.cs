namespace Timetabling.Common.SolutionModel
{
    public class EnrollmentState
    {
        public EnrollmentState(int configIndex, int[] classes)
        {
            ConfigIndex = configIndex;
            Classes = classes;
        }

        public readonly int ConfigIndex;

        public readonly int[] Classes;
    }
}